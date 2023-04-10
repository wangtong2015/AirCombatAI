using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aircraft
{
    public enum AircraftAgentPolicy
    {
        Default, // 默认，无策略
        Player, // 玩家
        Greedy, // 贪婪策略
        Gravity // 引力策略
    }

    /// <summary>
    /// 飞行器：注意，由于空战场景特殊，应该由Manager来统一协调处理EndEpisode，不在这里调用EndEpisode
    /// </summary>
    public class AircraftAgent : Agent
    {
        [Tooltip("ID")] public int ID = 0;

        [Tooltip("飞行器飞行逻辑")] public AircraftAgentPolicy policy = AircraftAgentPolicy.Default;

        [Tooltip("战队/红0蓝1")] public TeamSide teamSide = TeamSide.Red;
        
        [Tooltip("初始剩余子弹数量")] public int initBulletCount = 1000;
        [Tooltip("子弹预制体")] public GameObject bulletPrefab;
        [Tooltip("发射点")] public Transform bulletFirePoint;
        [Tooltip("发射间隔")] public float bulletFireInterval = 0.1f;
        [Tooltip("定义飞机的自动发射角度（在预设模式下生效）")] public float bulletFireAngle = 10f;

        [Tooltip("摄像机前置点")] public Transform cameraPointFront;
        [Tooltip("摄像机后置点")] public Transform cameraPointBack;
        
        [Header("Movement Parameters")] public float thrust = 25f;

        [Tooltip("定义加速倍数，用于增加飞机的速度")] public float boostMultiplier = 5f;

        [Tooltip("定义飞机的俯仰速度（x轴旋转）")] public float pitchSpeed = 30f;
        
        [Tooltip("定义飞机的偏航速度（y轴旋转）")] public float yawSpeed = 30f;

        [Tooltip("定义飞机的滚转速度（z轴旋转）")] public float rollSpeed = 30f;

        // 最多可以支持10 V 10
        const int MAX_N = 10;

        [Header("Explosion Stuff")] [Tooltip("The aircraft mesh that will disappear on explosion")]
        public GameObject meshObject;

        [Tooltip("爆炸特效Prefab")] public GameObject explosionEffectPrefab;


        [Header("操控绑定")] [Tooltip("上下")] public InputAction pitchInput;
        [Tooltip("左右")] public InputAction yawInput;
        [Tooltip("翻滚")] public InputAction rollInput;
        [Tooltip("加速")] public InputAction boostInput;
        [Tooltip("发射子弹")] public InputAction fireInput;

        protected AircraftArea area { get; private set; }
        protected Rigidbody rigidBody { get; private set; }
        protected TrailRenderer trail { get; private set; }

        // Whether  the aircraft is frozen (intentionally not flying)
        private bool frozen = false;

        // Controls
        private float pitchChange = 0f; // 俯仰变化量
        private float smoothPitchChange = 0f; // 平滑后的俯仰变化量

        private float yawChange = 0f; // 偏航变化量
        private float smoothYawChange = 0f; // 平滑后的偏航变化量

        private float rollChange = 0f; // 滚转变化量
        private float smoothRollChange = 0f; // 平滑后的滚转变化量

        private float boostChange = 0f; // 加速速度
        private float smoothBoostChange = 0f; // 平滑后的滚转变化量

        private float fireChange = 0f;
        private bool fire = false; // 是否发射子弹

        public int restBulletCount { get; private set; } = 0; // 剩余子弹数量

        private int hitBoundaryCount = 0; // 撞击到墙次数
        private int hitBulletCount = 0; // 撞击到子弹次数
        private int hitAgentCount = 0; // 撞击到其他飞行器次数

        // 该飞行器是否被摧毁了
        private bool _isDestroyed = false;

        public bool IsDestroyed
        {
            get { return _isDestroyed; }
            set
            {
                var isChange = value != _isDestroyed;
                _isDestroyed = value;
                meshObject.SetActive(!value);

                if (!area.trainingMode && !value && isChange)
                {
                    // 非训练模式下才会展示摧毁动画
                    PlayExplosion();
                }
            }
        }

        public string Name => $"AircraftAgent-{area.ID}-{teamSide}-{ID}";

        protected override void Awake()
        {
            // 获取飞行区域、刚体和拖尾组件的引用
            area = GetComponentInParent<AircraftArea>();
            rigidBody = GetComponent<Rigidbody>();
            trail = GetComponent<TrailRenderer>();
            restBulletCount = initBulletCount;
            if (policy == AircraftAgentPolicy.Player)
            {
                pitchInput.Enable();
                yawInput.Enable();
                rollInput.Enable();
                boostInput.Enable();
                fireInput.Enable();
            }
        }

        /// <summary>
        /// 当Agent首次初始化时调用
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            // Override the max step set in the inspector
            // Max 5000 steps if training, infinite steps if racing
            MaxStep = area.trainingMode ? 5000 : 0;
        }

        /// <summary>
        /// Called when a new episode begins
        /// </summary>
        public override void OnEpisodeBegin()
        {
            base.OnEpisodeBegin();
            DebugLog("OnEpisodeBegin");
            // Reset the velocity, position, and orientation
            Reset();
        }

        public void Reset()
        {
            IsDestroyed = false;
            rigidBody.velocity = Vector3.zero;
            rigidBody.angularVelocity = Vector3.zero;
            trail.emitting = false;
            area.ResetAgentPosition(agent: this);
            restBulletCount = initBulletCount;
            if (!area.trainingMode)
            {
                meshObject.SetActive(true);
            }
        }

        private void DebugLog(string info)
        {
            Debug.Log(
                $"{Name}:{info} frozen={frozen} reward={GetCumulativeReward()} bulletCount={restBulletCount} StepCount={StepCount} hit={hitBoundaryCount}:{hitBulletCount}:{hitAgentCount}");
        }


        /// <summary>
        /// Collects observations used by agent to make decisions
        /// </summary>
        /// <param name="sensor">The vector sensor</param>
        public override void CollectObservations(VectorSensor sensor)
        {
            Debug.Assert(area.AircraftAgents.Count > 0, "area.AircraftAgents.Count > 0");

            // 距离边界的距离
            // 自己的速度
            Debug.Log($"自己的速度={transform.InverseTransformDirection(rigidBody.velocity)}");
            sensor.AddObservation(transform.InverseTransformDirection(rigidBody.velocity));
            // 自己的剩余装弹量
            sensor.AddObservation(restBulletCount);
            // 自己是否在发射子弹
            sensor.AddObservation(smoothPitchChange * pitchSpeed); // 当前pitch速度
            sensor.AddObservation(smoothYawChange * yawSpeed); // 当前yaw速度
            sensor.AddObservation(smoothRollChange * rollSpeed); // 当前roll速度
            sensor.AddObservation(fireChange);
            
            
            // 获取墙壁的相对位置
            foreach (var boundary in area.boundaries)
            {
                sensor.AddObservation(transform.InverseTransformPoint(boundary.transform.position));
                sensor.AddObservation(transform.InverseTransformDirection(boundary.transform.up));
            }

            int otherAgentCount = 0;
            
            foreach (var agent in area.AircraftAgents)
            {
                if (agent != this)
                {
                    // 其他战机的相对位置
                    sensor.AddObservation(transform.InverseTransformPoint(agent.transform.position));
                    // 其他战机的相对速度
                    sensor.AddObservation(transform.InverseTransformDirection(agent.rigidBody.velocity));
                    
                    // 其他战机的世界旋转角度转换为相对 自己 的旋转角度
                    Quaternion relativeRotation = Quaternion.Inverse(transform.rotation) * agent.transform.rotation;
                    Debug.Log("a 相对于 b 的旋转角度：" + relativeRotation);
                    sensor.AddObservation(relativeRotation);
                    
                    
                    // 敌机是否在我的攻击范围内
                    // 我是否在敌机的攻击范围内
                    // 敌机是否在发射子弹
                    sensor.AddObservation(agent.fireChange);
                    // 友方战机 1 敌方战机 -1 不存在 0
                    sensor.AddObservation(agent.teamSide == teamSide ? 1 : -1);
                    otherAgentCount += 1;
                    if (otherAgentCount >= MAX_N - 1)
                    {
                        break;
                    }
                }
            }

            // 填充剩下的空间
            for (int i = otherAgentCount; i < MAX_N - 1; i++)
            {
                // 敌机的相对位置
                sensor.AddObservation(Vector3.zero);
                // 敌机的相对速度
                sensor.AddObservation(Vector3.zero);
                // 敌机是否在我的攻击范围内
                // 我是否在敌机的攻击范围内
                // 敌机是否在发射子弹
                sensor.AddObservation(0);
                // 友方战机 1 敌方战机 -1 不存在 0
                sensor.AddObservation(0);
            }

            // Total Observations = 3 + 1 + 4 + 8 * N + 6 * 6= 44 + 8 * N
            // 1v1 52
            // N最多为10 44 + 80 = 124
        }
        
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // base.Heuristic(actionsOut);
            ActionSegment<int> vectorActions = actionsOut.DiscreteActions;
            float pitchValue = 0f, yawValue = 0f, rollValue = 0f, boostValue = 0f, fireValue = 0f;
            // Pitch: 1 == turn down, 0 == none, -1 == turn up
            // Yaw: 1 == turn right, 0 == none, -1 == turn left
            // Roll: 1 == roll right, 0 == none, -1 == roll left
            // Boost: 1 == bost, 0 == no boost

            if (policy == AircraftAgentPolicy.Player)
            {
                pitchValue = Mathf.Round(pitchInput.ReadValue<float>());
                yawValue = Mathf.Round(yawInput.ReadValue<float>());
                rollValue = Mathf.Round(rollInput.ReadValue<float>());
                boostValue = Mathf.Round(boostInput.ReadValue<float>());
                fireValue = Mathf.Round(fireInput.ReadValue<float>());
            }
            else if (policy == AircraftAgentPolicy.Greedy)
            {
                // 获取所有智能体和墙壁
                List<GameObject> walls = area.boundaries;

                // 找到不同阵营的敌方智能体
                List<AircraftAgent> aliveEnemies = area.FindAliveEnemies();
                // 如果有敌人，就自动向敌人发射子弹
                if (aliveEnemies.Count > 0)
                {
                    // 检查是否有敌机在自己的正前方
                    var (enemy, enemyAngle) = area.FindPlayerTargetEnemy();
                    if (!ReferenceEquals(enemy, null))
                    {
                        // 如果敌人距离在发射范围内，则发射子弹
                        if (enemyAngle <= bulletFireAngle)
                        {
                            // 瞄准敌人
                            fireValue = 1f;
                        }
                        
                        Vector3 targetDirection = enemy.transform.position - transform.position;
                        // 需要旋转的角度
                        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                        // 将四元数转换为欧拉角
                        Vector3 eulerAngles = targetRotation.eulerAngles;
                        // 判断需要向左或向右旋转多少度
                        float horizontalAngle = Mathf.DeltaAngle(transform.eulerAngles.y, eulerAngles.y);
                        // 判断需要向上或向下旋转多少度
                        float verticalAngle = Mathf.DeltaAngle(transform.eulerAngles.x, eulerAngles.x);

                        // 需要转向战机
                        // 计算敌机和我方战机的相对位置
                        // 转向敌机
                        var smoothYawSpeed = smoothYawChange * yawSpeed;
                        var smoothPitchSpeed = smoothPitchChange * pitchSpeed;
                        var needYawTime = 0f;
                        var needPitchTime = 0f;
                        if (smoothYawSpeed != 0)
                        {
                            needYawTime = horizontalAngle / smoothYawSpeed;
                        }

                        if (smoothPitchSpeed != 0)
                        {
                            needPitchTime = verticalAngle / smoothPitchSpeed;
                        }
                        DebugLog($"Heuristic horizontalAngle={horizontalAngle} verticalAngle={verticalAngle} smoothYawSpeed={smoothYawSpeed} smoothPitchSpeed={smoothPitchSpeed} needYawTime={needYawTime} needPitchTime={needPitchTime}");

                        float threshold = .1f;
                        if (horizontalAngle > 0f)
                        {
                            // 敌方飞行器在右边
                            if (smoothYawSpeed > 0 && horizontalAngle / smoothYawSpeed < threshold)
                            {
                                // 已经再向右转了且0.5s内就能转过去，则不再设置向右转
                                yawValue = 0f;
                            }
                            else
                            {
                                yawValue = 1f;
                            }
                        }
                        else if (horizontalAngle < 0f)
                        {
                            if (smoothYawSpeed < 0 && horizontalAngle / smoothYawSpeed < threshold)
                            {
                                // 已经再向左转了且0.5s内就能转过去，则不再设置向右转
                                yawValue = 0f;
                            }
                            else
                            {
                                // 左转
                                yawValue = -1f;
                            }
                        }
                                                
                        if (verticalAngle > 0f)
                        {
                            // 向下
                            if (smoothPitchSpeed > 0 && verticalAngle / smoothPitchSpeed < threshold)
                            {
                                // 已经再向下转了且0.5s内就能转过去，则不再设置向下转
                                pitchValue = 0f;
                            }
                            else
                            {
                                pitchValue = 1f;
                            }
                        }
                        else if (verticalAngle < 0f)
                        {
                            // 向上
                            if (smoothPitchSpeed < 0 && verticalAngle / smoothPitchSpeed < threshold)
                            {
                                // 已经再向上转了且0.5s内就能转过去，则不再设置向上转
                                pitchValue = 0f;
                            }
                            else
                            {
                                pitchValue = -1f;
                            }
                        }
                    }
                }
                
                // 避免撞到墙壁
                foreach (GameObject wall in walls)
                {
                    if (Vector3.Distance(transform.position, wall.transform.position) < 35f)
                    {
                        // 墙壁在智能体前方
                        if (Vector3.Dot(transform.forward, wall.transform.position - transform.position) > 0)
                        {
                            // 向右转向
                            yawValue = 1f;
                        }
                        // 墙壁在智能体后方
                        else
                        {
                            // 向左转向
                            yawValue = -1f;
                        }
                    }
                }
            }
            else if (policy == AircraftAgentPolicy.Gravity)
            {
                // TODO: 引力策略 
                // 敌机是斥力模型，距离越近斥力越强
                // 选择夹角最小的敌机作为目标敌机，产生吸引力，越远引力越强（弹簧）
                // 墙壁是斥力模型，距离越近斥力越强
                // 友方战机是斥力模型，距离越近斥力越强
                // 敌机发射点具有斥力，机尾具有引力（要避开敌机的机头）

                // 获取所有智能体和墙壁
                List<AircraftAgent> agents = area.AircraftAgents;
                List<GameObject> walls = area.boundaries;

                // 找到不同阵营的敌方智能体
                List<AircraftAgent> enemies =
                    agents.Where(a => a != this && a.teamSide != teamSide && a.gameObject.activeSelf).ToList();

                // 如果有敌人，就自动向敌人发射子弹
                if (enemies.Count > 0)
                {
                    // 检查是否有敌机在自己的正前方
                    AircraftAgent minAngleEnemy = null;
                    float minEnemyAngle = Mathf.Infinity;
                    foreach (AircraftAgent enemy in enemies)
                    {
                        float angle = Vector3.Angle(transform.forward, enemy.transform.position - transform.position);

                        if (angle < minEnemyAngle)
                        {
                            minEnemyAngle = angle;
                            minAngleEnemy = enemy;
                        }
                    }

                    // 如果敌人距离在发射范围内，则发射子弹
                    if (minEnemyAngle <= bulletFireAngle)
                    {
                        // 瞄准敌人
                        fireValue = 1f;
                    }

                    if (!ReferenceEquals(minAngleEnemy, null))
                    {
                        // 选择夹角最小的敌机作为目标敌机，产生吸引力，越远引力越强（弹簧）
                        Vector3 relativePos = transform.InverseTransformPoint(minAngleEnemy.transform.position);
                    }
                }

                // 避免撞到墙壁
                foreach (GameObject wall in walls)
                {
                    if (Vector3.Distance(transform.position, wall.transform.position) < 35f)
                    {
                        // 墙壁在智能体前方
                        if (Vector3.Dot(transform.forward, wall.transform.position - transform.position) > 0)
                        {
                            // 向右转向
                            yawValue = 1f;
                        }
                        // 墙壁在智能体后方
                        else
                        {
                            // 向左转向
                            yawValue = -1f;
                        }
                    }
                }
            }

            // convert -1 (down) to discrete value 2
            if (pitchValue == -1f)
            {
                pitchValue = 2f;
            }

            // convert -1 (left) to discrete value 2
            if (yawValue == -1f)
            {
                yawValue = 2f;
            }

            if (rollValue == -1f)
            {
                rollValue = 2f;
            }

            vectorActions[0] = Mathf.RoundToInt(pitchValue);
            vectorActions[1] = Mathf.RoundToInt(yawValue);
            vectorActions[2] = Mathf.RoundToInt(rollValue);
            vectorActions[3] = Mathf.RoundToInt(boostValue);
            vectorActions[4] = Mathf.RoundToInt(fireValue);
        }


        /// <summary>
        /// Read action inputs from vectorAction
        /// </summary>
        /// <param name="actions">actions长度为4</param>
        public override void OnActionReceived(ActionBuffers actions)
        {
            if (frozen)
            {
                return;
            }

            ActionSegment<int> vectorActions = actions.DiscreteActions;

            pitchChange = vectorActions[0]; // 0 1 2
            yawChange = vectorActions[1]; // 0 1 2
            rollChange = vectorActions[2]; // 0 1 2
            boostChange = vectorActions[3]; // 0 1
            fireChange = vectorActions[4]; // 0 1

            DebugLog(
                $"OnActionReceived pitchChange={pitchChange} yawChange={yawChange} rollChange={rollChange} boostChange={boostChange} fireChange={fireChange}");

            if (pitchChange == 2f)
            {
                pitchChange = -1;
            }

            if (yawChange == 2f)
            {
                yawChange = -1;
            }

            if (rollChange == 2f)
            {
                rollChange = -1;
            }

            if (boostChange > 0 && !trail.emitting)
            {
                trail.Clear();
            }

            fire = fireChange > 0;

            trail.emitting = boostChange > 0;

            ProcessMovement();
            if (MaxStep > 0)
            {
                // Small negative reward every step
                AddReward(-1f / MaxStep);
            }
        }

        private void ProcessMovement()
        {
            // 前进
            smoothBoostChange = Mathf.MoveTowards(smoothBoostChange, boostChange, 2f * Time.fixedDeltaTime);

            float boostModifier = 1 + (boostMultiplier - 1) * smoothBoostChange;
            rigidBody.AddRelativeForce(Vector3.forward * (thrust * boostModifier), ForceMode.Force);

            // 旋转
            // Calculate smooth deltas
            smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
            smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
            smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);

            transform.Rotate(smoothPitchChange * Time.fixedDeltaTime * pitchSpeed,
                smoothYawChange * Time.fixedDeltaTime * yawSpeed, -smoothRollChange * Time.fixedDeltaTime * rollSpeed,
                Space.Self);

            if (fire)
            {
                Fire();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.transform.CompareTag("Boundary"))
            {
                // 撞击到墙
                hitBoundaryCount += 1;
            }

            // 碰撞到其他物体，摧毁自己
            StartCoroutine(DestroyAfterOneFrame());
        }

        private void OnTriggerEnter(Collider other)
        {
            DebugLog($"OnTriggerEnter-{other.tag}");
            if (other.transform.CompareTag("Agent"))
            {
                // 其他物体是Agent
                var otherAgent = other.gameObject.GetComponent<AircraftAgent>();
                if (otherAgent.IsDestroyed)
                {
                    // 这个Agent已经被摧毁了，就直接穿过去不处理
                    return;
                }

                hitAgentCount += 1;
            }
            else if (other.transform.CompareTag("Bullet"))
            {
                // 被子弹击中
                var bullet = other.gameObject.GetComponent<AircraftBullet>();
                if (bullet.fireAgent == this)
                {
                    // 撞击到自己发射的子弹了，不处理
                    return;
                }

                hitBulletCount += 1;
            }
            else if (other.transform.CompareTag("Boundary"))
            {
                // 撞击到墙
                hitBoundaryCount += 1;
            }

            // 碰撞到其他物体，摧毁自己
            StartCoroutine(DestroyAfterOneFrame());
        }


        /// <summary>
        /// 等待一帧再摧毁自己，方便对撞的时候处理
        /// </summary>
        /// <returns></returns>
        private IEnumerator DestroyAfterOneFrame()
        {
            yield return null; // 等待一帧
            IsDestroyed = true;
            // 被摧毁的情况下给予惩罚
            AddReward(-10);
            if (!area.trainingMode)
            {
                // 非训练模式下
                FreezeAgent();
            }
        }


        /// <summary>
        /// 冻结物体，此时物体无法移动，碰撞体也暂时关闭
        /// </summary>
        public void FreezeAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in training");
            if (frozen)
            {
                return;
            }

            DebugLog($"FreezeAgent");

            frozen = true;
            rigidBody.Sleep();
            trail.emitting = false;
        }

        /// <summary>
        /// Resume agent movement and actions
        /// </summary>
        public void ThawAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in training");

            if (!frozen)
            {
                return;
            }

            DebugLog($"ThawAgent");

            frozen = false;
            rigidBody.WakeUp();
        }

        private float lastFireTime = 0;

        private void Fire()
        {
            DebugLog($"Fire");

            if (restBulletCount <= 0)
            {
                // 子弹已经用完了，给出惩罚
                AddReward(-0.5f);
                return;
            }

            if (Time.time - lastFireTime < bulletFireInterval)
            {
                return;
            }

            lastFireTime = Time.time;
            GameObject node = Instantiate(bulletPrefab, null);
            AircraftBullet bullet = node.GetComponent<AircraftBullet>();
            bullet.fireAgent = this;
            node.transform.position = bulletFirePoint.position;
            node.transform.rotation = bulletFirePoint.rotation;

            restBulletCount -= 1;
            // 发射子弹给点奖励，鼓励发射子弹
            AddReward(1f / initBulletCount);
        }

        /// <summary>
        /// 自己发射的子弹击中了战机
        /// </summary>
        /// <param name="enemy">被击中的敌机</param>
        public void OnBulletHit(AircraftAgent target)
        {
            Debug.Log($"{Name} OnBulletHit CumulativeReward={GetCumulativeReward()}");
            if (target.teamSide == this.teamSide)
            {
                // 击中友方战机，惩罚
                AddReward(-10);
            }
            else
            {
                // 击中敌方战机，奖励
                AddReward(10);
            }
        }

        /// <summary>
        /// 平局
        /// </summary>
        public void OnDraw()
        {
            DebugLog($"OnDraw");

            // 平局奖励
            AddReward(-5);
        }

        /// <summary>
        /// 胜利奖励
        /// </summary>
        public void OnWin()
        {
            DebugLog($"OnWin");

            AddReward(10);
        }

        /// <summary>
        /// 失败
        /// </summary>
        public void OnLose()
        {
            DebugLog($"OnLose");
            // 失败
            AddReward(-10);
        }
        
        private void PlayExplosion()
        {
            GameObject explosion = Instantiate(explosionEffectPrefab, transform);
            Destroy(explosion, explosion.GetComponent<ParticleSystem>().main.duration); // 在动画特效播放完毕后销毁特效
        }
        
        private void OnDestroy()
        {
            DebugLog($"OnDestroy");

            if (policy == AircraftAgentPolicy.Player)
            {
                pitchInput.Disable();
                yawInput.Disable();
                rollInput.Disable();
                boostInput.Disable();
                fireInput.Disable();
            }
        }
    }
}