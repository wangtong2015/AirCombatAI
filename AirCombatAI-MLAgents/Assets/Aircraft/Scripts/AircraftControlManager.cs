using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Aircraft
{
    /// <summary>
    /// 1 v 1 空战操控场景，仅限非训练模式使用
    /// </summary>
    public class AircraftControlManager : MonoBehaviour
    {
        [Tooltip("游戏区域")] public AircraftArea area { get; private set; }
        [Tooltip("暂停")] public InputAction pauseInput;
        
        private void Awake()
        {
            area = FindObjectOfType<AircraftArea>();
        }
        
        void Start()
        {
            pauseInput.Enable();
            pauseInput.performed += PauseInputPerformed;

            GameManager.Instance.GameState = GameState.Playing;
            
            StartCoroutine(StartAIFlight());
        }
        
        /// <summary>
        /// 开始计算比赛结果
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartAIFlight()
        {
            while (true)
            {
                area.DebugLog("计算状态");
                // 双方剩余战机的数量
                var redRestCount = 0; // 红方剩余战机数
                var blueRestCount = 0; // 蓝方剩余战机数
                var redRestBulletCount = 0; // 红方剩余子弹数
                var blueRestBulletCount = 0; // 蓝方剩余子弹数
                AircraftResult result = AircraftResult.Default;
            
                foreach (var agent in area.AircraftAgents)
                {
                    if (!agent.IsDestroyed)
                    {
                        if (agent.teamSide == TeamSide.Red)
                        {
                            redRestCount += 1;
                            redRestBulletCount += agent.restBulletCount;
                        }
                        else
                        {
                            blueRestCount += 1;
                            blueRestBulletCount += agent.restBulletCount;
                        }
                    }
                }
            
                if (redRestCount == 0 && blueRestCount == 0)
                {
                    // 双方平局
                    result = AircraftResult.Draw;
                }
                else if (redRestCount == 0)
                {
                    // 蓝方获胜
                    result = AircraftResult.BlueWin;
                }
                else if (blueRestCount == 0)
                {
                    // 红方获胜
                    result = AircraftResult.RedWin;
                }
                else if (redRestBulletCount == 0 && blueRestBulletCount == 0)
                {
                    // 双方子弹都打光了，平局
                    result = AircraftResult.Draw;
                }
                else if (redRestBulletCount == 0)
                {
                    // 红方没有子弹了，蓝方获胜
                    result = AircraftResult.BlueWin;
                }
                else if (blueRestBulletCount == 0)
                {
                    // 蓝方没有子弹了，红方获胜
                    result = AircraftResult.RedWin;
                }
                
                if (result != AircraftResult.Default)
                {
                    // 游戏结束
                    switch (result)
                    {
                        case AircraftResult.Draw:
                        {
                            // 平局
                            foreach (var agent in area.AircraftAgents)
                            {
                                agent.OnDraw();
                            }

                            GameManager.Instance.GameState = GameState.Draw;
                            break;
                        }
                        case AircraftResult.BlueWin:
                        {
                            // 蓝方胜利
                            foreach (var agent in area.AircraftAgents)
                            {
                                if (agent.teamSide == TeamSide.Blue)
                                {
                                    agent.OnWin();
                                }
                                else
                                {
                                    agent.OnLose();
                                }
                            }


                            if (area.player.teamSide == TeamSide.Red)
                            {
                                GameManager.Instance.GameState = GameState.Lose;
                            }
                            else
                            {
                                GameManager.Instance.GameState = GameState.Win;
                            }

                            break;
                        }
                        case AircraftResult.RedWin:
                        {
                            // 红方胜利
                            foreach (var agent in area.AircraftAgents)
                            {
                                if (agent.teamSide == TeamSide.Red)
                                {
                                    agent.OnWin();
                                }
                                else
                                {
                                    agent.OnLose();
                                }
                            }

                            if (area.player.teamSide == TeamSide.Blue)
                            {
                                GameManager.Instance.GameState = GameState.Lose;
                            }
                            else
                            {
                                GameManager.Instance.GameState = GameState.Win;
                            }

                            break;
                        }
                    }
                    
                    yield return new WaitForSeconds(2f);

                    foreach (var agent in area.AircraftAgents)
                    {
                        agent.Reset();
                    }

                    GameManager.Instance.GameState = GameState.Playing;
                    ThawAllAgents();
                    
                    yield return new WaitForSeconds(1f);
                }

                // 每隔0.1s刷新一次
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        /// <summary>
        /// 冻结所有飞行器
        /// </summary>
        public void FreezeAllAgents()
        {
            foreach (var agent in area.AircraftAgents)
            {
                agent.FreezeAgent();
            }
        }
        
        /// <summary>
        /// 解冻所有飞行器
        /// </summary>
        public void ThawAllAgents()
        {
            foreach (var agent in area.AircraftAgents)
            {
                agent.ThawAgent();
            }
        }
        
    
        private void FixedUpdate()
        {
            if (ReferenceEquals(GameManager.Instance, null))
            {
                return;
            }
            // 展示对线结束
            switch (GameManager.Instance.GameState)
            {
                case GameState.Playing:
                {
                    ThawAllAgents();
                    break;
                }
                case GameState.Paused:
                {
                    FreezeAllAgents();
                    break;
                }
                case GameState.Win:
                {
                    FreezeAllAgents();
                    break;
                }
                case GameState.Lose:
                {
                    FreezeAllAgents();
                    break;
                }
                case GameState.Draw:
                {
                    FreezeAllAgents();
                    break;
                }
                default:
                {
                    ThawAllAgents();
                    break;
                }
            }
        }
        
        /// <summary>
        /// 暂停
        /// </summary>
        /// <param name="obj">The callback context</param>
        private void PauseInputPerformed(InputAction.CallbackContext obj)
        {
            Debug.Log($"PauseInputPerformed GameState={GameManager.Instance.GameState}");
            if (GameManager.Instance.GameState == GameState.Playing)
            {
                GameManager.Instance.GameState = GameState.Paused;
            }
            else
            {
                GameManager.Instance.GameState = GameState.Playing;
            }
        }
        
        private void OnDestroy()
        {
            pauseInput.performed -= PauseInputPerformed;
            pauseInput.Disable();
        }
    }
}