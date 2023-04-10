using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;
using Unity.MLAgents;
using Unity.VisualScripting;

namespace Aircraft
{
    public class AircraftArea : MonoBehaviour
    {
        [Tooltip("ID")] public int ID = 0;
        [Tooltip("是否是训练模式")] public bool trainingMode = false; // 是否是训练模式


        [Tooltip("红方出生点")] public GameObject redBornPoint;
        [Tooltip("蓝方出生点")] public GameObject blueBornPoint;

        [Tooltip("边界")] public List<GameObject> boundaries;

        private Vector3 redBornPointPosition;
        private Vector3 blueBornPointPosition;

        public List<AircraftAgent> AircraftAgents { get; private set; }

        public AircraftAgent player { get; private set; }

        public string Name => $"AircraftArea-{ID}";

        public void DebugLog(string info)
        {
            Debug.Log($"{Name}:{info} StepCount={AircraftAgents[0].StepCount} State={GameManager.Instance.GameState}");
        }

        private void Awake()
        {
            AircraftAgents = transform.GetComponentsInChildren<AircraftAgent>().ToList();
            Debug.Assert(AircraftAgents.Count >= 2, "必须至少有两架飞机");
            Debug.Assert(AircraftAgents.FindAll(it => it.policy == AircraftAgentPolicy.Player).Count <= 1, "最多只能有一架玩家控制的战机");
            
            redBornPointPosition = redBornPoint.transform.position;
            blueBornPointPosition = blueBornPoint.transform.position;
            redBornPoint.SetActive(false);
            blueBornPoint.SetActive(false);
            
            foreach (var agent in AircraftAgents)
            {
                if (agent.policy == AircraftAgentPolicy.Player)
                {
                    player = agent;
                    break;
                }
            }

            if (player == null)
            {
                foreach (var agent in AircraftAgents)
                {
                    if (agent.ID == 0 && agent.teamSide == TeamSide.Red)
                    {
                        player = agent;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 重置智能体的位置
        /// </summary>
        /// <param name="agent">要重置的智能体</param>
        public void ResetAgentPosition(AircraftAgent agent)
        {
            if (agent.teamSide == TeamSide.Red)
            {
                // 重置回红方出生点
                // 在出生点附近随机初始化位置
                Vector3 position = redBornPointPosition;
                position.x += Random.Range(-10, 10);
                position.z += Random.Range(-10, 10);
                position.y += Random.Range(-10, 10);
                agent.transform.position = position;
                agent.transform.rotation = Quaternion.Euler(0, Random.Range(-180, 180), 0);
            }
            else
            {
                // 重置回蓝方出生点
                // 在出生点附近随机初始化位置
                Vector3 position = blueBornPointPosition;
                position.x += Random.Range(-10, 10);
                position.z += Random.Range(-10, 10);
                position.y += Random.Range(-10, 10);
                agent.transform.position = position;
                agent.transform.rotation = Quaternion.Euler(0, Random.Range(-180, 180), 0);
            }
        }

        public List<AircraftAgent> FindAliveAgents()
        {
            return AircraftAgents.FindAll(it => !it.IsDestroyed);
        }
        
        /// <summary>
        /// 找到当前还没被摧毁的敌方战机
        /// </summary>
        /// <returns></returns>
        public List<AircraftAgent> FindAliveEnemies()
        {
            if (ReferenceEquals(player, null))
            {
                return null;
            }

            return AircraftAgents.FindAll(it => !it.IsDestroyed && it.teamSide != player.teamSide);
        }

        /// <summary>
        /// 找到当前还没有被摧毁的友方战机
        /// </summary>
        /// <returns></returns>
        public List<AircraftAgent> FindAliveFriends()
        {
            if (ReferenceEquals(player, null))
            {
                return null;
            }

            return AircraftAgents.FindAll(it => !it.IsDestroyed && it.teamSide == player.teamSide);
        }
        
        /// <summary>
        /// 找到当前player前进向量与敌方相对位置夹角最小的敌方战机
        /// </summary>
        /// <returns></returns>
        public (AircraftAgent, float) FindPlayerTargetEnemy()
        {
            if (ReferenceEquals(player, null))
            {
                return (null, 0f);
            }
            
            AircraftAgent targetEnemy = null;
            float minAngle = float.MaxValue;
            foreach (var enemy in FindAliveEnemies())
            {
                float angle = Vector3.Angle(player.transform.forward, enemy.transform.position - player.transform.position);
                if (angle < minAngle)
                {
                    minAngle = angle;
                    targetEnemy = enemy;
                }
            }
            
            return (targetEnemy, minAngle);
        }
    }
}