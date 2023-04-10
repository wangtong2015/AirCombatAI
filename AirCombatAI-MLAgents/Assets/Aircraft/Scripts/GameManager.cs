using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aircraft
{
    [Serializable]
    public enum GameState
    {
        Default,
        MainMenu,
        Playing,

        // 训练模式下没有
        Paused,
        Win, // 胜利
        Lose, // 失败：对面撞墙或者被击中
        Draw, // 平局：对战双方互撞
    }

    public enum AircraftResult
    {
        Default,
        RedWin, // 红方获胜
        BlueWin, // 蓝方获胜
        Draw, // 双方平局
    }
    
    [Serializable]
    public enum TeamSide
    {
        Red,
        Blue
    }
    
    public delegate void OnStateChangeHandler(GameState state);

    public class GameManager : MonoBehaviour
    {
        
        /// <summary>  
        /// Event is called when the game state changes
        /// </summary>
        public event OnStateChangeHandler OnStateChange;

        private GameState _gameState;

        /// <summary>  
        /// The current game state
        /// </summary>
        public GameState GameState
        {
            get { return _gameState; }
            set
            {
                var isChange = _gameState != value;
                _gameState = value;
                if (OnStateChange != null && isChange)
                {
                    OnStateChange(value);
                }
            }
        }
        
        /// <summary>  
        /// The singleton GameManager instance
        /// </summary>
        public static GameManager Instance { get; private set; }
    
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
                // 全屏  
                // Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
            Instance = null;
        }

        /// <summary>  
        /// Loads a new scene and sets the game state
        /// /// </summary>
        /// /// <param name="sceneName">The scene to load</param>
        /// /// <param name="newState">The new game state</param>
        public void LoadScene(string sceneName, GameState newState)
        {
            StartCoroutine(LoadSceneAsync(sceneName, newState));
        }

        private IEnumerator LoadSceneAsync(string sceneName, GameState newState)
        {
            // Load the new level  
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (operation.isDone == false)
            {
                yield return null;
            } // Set the resolution  

            //
            // Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
            // Update the game state  
            GameState = newState;
        }
        
        /// <summary>
        /// 重新加载当前场景
        /// </summary>
        public void OnResumeButtonClicked()
        {
            LoadScene(SceneManager.GetActiveScene().name, GameState.Playing);
        }
    }
}