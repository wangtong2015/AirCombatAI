using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aircraft
{
    public class AircraftLearningManager : MonoBehaviour
    {
        [Tooltip("游戏区域")] public AircraftArea area;

        private void Awake()
        {
            if (area.trainingMode == false)
            {
                throw new Exception("只能在训练模式下使用");
            }
        }
        
        // Start is called before the first frame update
        public int RedWins { get; private set; } = 0; // 红方获胜次数
        public int BlueWins { get; private set; } = 0; // 蓝方获胜次数
        public int Draws { get; private set; } = 0; // 平局次数
        
        
        private void FixedUpdate()
        {
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
            
            switch (result)
            {
                case AircraftResult.Draw:
                {
                    // 平局
                    foreach (var agent in area.AircraftAgents)
                    {
                        agent.OnDraw();
                    }

                    Draws += 1;

                    area.DebugLog($"空战结果:平局 {RedWins}:{BlueWins}:{Draws}");
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

                    BlueWins += 1;
                    area.DebugLog($"空战结果:蓝方获胜 {RedWins}:{BlueWins}:{Draws}");

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

                    RedWins += 1;
                    area.DebugLog($"空战结果:红方获胜 {RedWins}:{BlueWins}:{Draws}");
                    break;
                }
            }
            
            if (result != AircraftResult.Default)
            {
                foreach (var agent in area.AircraftAgents)
                {
                    agent.EndEpisode();
                }
            }
        }
    }
}