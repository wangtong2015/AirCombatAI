using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Aircraft;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = System.Object;

/// <summary>
/// 仅限在玩家模式下使用
/// </summary>
public class AircraftHUDController : MonoBehaviour
{
    [Tooltip("信息文本")] public TextMeshProUGUI infoText;
    [Tooltip("敌方指示器")] public GameObject enemyIndicator;


    [Tooltip("At what point to show an arrow toward the checkpoint, rather than the icon centered on it")]
    public float indicatorLimit = .7f;

    [Tooltip("胜利文本")] public TextMeshProUGUI winText;
    [Tooltip("失败文本")] public TextMeshProUGUI loseText;
    [Tooltip("失败文本")] public TextMeshProUGUI drawText;

    [Tooltip("主相机")] public Camera MainCamera;
    [Tooltip("副相机")] public Camera ViceCamera;

    [Tooltip("游戏区域")] public AircraftArea area;
    [Tooltip("游戏操控控制器")] private AircraftControlManager manager;

    [Tooltip("菜单")] public GameObject pauseMenu;

    [Tooltip("敌方指示器列表")] private List<AircraftEnemyIndicator> enemyIndicatorList;

    private int winCount = 0;
    private int loseCount = 0;
    private int drawCount = 0;

    private void Awake()
    {
        manager = FindObjectOfType<AircraftControlManager>();
        enemyIndicatorList = enemyIndicator.GetComponentsInChildren<AircraftEnemyIndicator>().ToList();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChange += OnStateChange;
        }

        // Hide UI
        GameManager.Instance.GameState = GameState.Playing;
    }

    /// <summary>
    /// React to state changes
    /// </summary>
    private void OnStateChange(GameState state)
    {
        // 展示对线结束
        switch (state)
        {
            case GameState.Playing:
            {
                manager.ThawAllAgents();
                winText.gameObject.SetActive(false);
                loseText.gameObject.SetActive(false);
                drawText.gameObject.SetActive(false);
                pauseMenu.gameObject.SetActive(false);
                break;
            }
            case GameState.Paused:
            {
                manager.FreezeAllAgents();
                winText.gameObject.SetActive(false);
                loseText.gameObject.SetActive(false);
                drawText.gameObject.SetActive(false);
                pauseMenu.gameObject.SetActive(true);
                break;
            }
            case GameState.Win:
            {
                pauseMenu.gameObject.SetActive(false);
                winText.gameObject.SetActive(true);
                loseText.gameObject.SetActive(false);
                drawText.gameObject.SetActive(false);
                winCount += 1;
                break;
            }
            case GameState.Lose:
            {
                pauseMenu.gameObject.SetActive(false);
                winText.gameObject.SetActive(false);
                loseText.gameObject.SetActive(true);
                drawText.gameObject.SetActive(false);

                loseCount += 1;
                break;
            }
            case GameState.Draw:
                pauseMenu.gameObject.SetActive(false);
                winText.gameObject.SetActive(false);
                loseText.gameObject.SetActive(false);
                drawText.gameObject.SetActive(true);
                drawCount += 1;
                break;
            default:
            {
                winText.gameObject.SetActive(false);
                loseText.gameObject.SetActive(false);
                drawText.gameObject.SetActive(false);
                pauseMenu.gameObject.SetActive(false);
                break;
            }
        }
    }

    private void LateUpdate()
    {
        if (ReferenceEquals(area, null) || ReferenceEquals(area.player, null))
        {
            return;
        }

        var aliveEnemies = area.FindAliveEnemies();
        var aliveFriends = area.FindAliveFriends();
        
        // 更新相机位置
        MainCamera.transform.position = area.player.cameraPointFront.position;
        MainCamera.transform.rotation = area.player.cameraPointFront.rotation;

        ViceCamera.transform.position = area.player.cameraPointBack.position;
        ViceCamera.transform.rotation = area.player.cameraPointBack.rotation;


        int enemyIndex = 0;

        foreach (var agent in aliveEnemies)
        {
            // 是敌方且还没被摧毁
            if (enemyIndicatorList.Count > enemyIndex)
            {
                UpdateArrow(enemyIndicatorList[enemyIndex], agent);
            }

            enemyIndex += 1;
        }
        
        // 剩余指示器的隐藏
        for (var i = enemyIndex; i < enemyIndicatorList.Count; i++)
        {
            enemyIndicatorList[i].gameObject.SetActive(false);
        }

        // 计算最小边界距离
        float minBoundaryDistance = float.MaxValue;

        foreach (var areaBoundary in area.boundaries)
        {
            // 获取物体的位置
            Vector3 objectPosition = area.player.transform.position;
            // 获取平面的法线向量和某一点
            Vector3 pointOnPlane = areaBoundary.transform.position; // 平面上的某一点
            Vector3 planeNormal = areaBoundary.transform.up; // 平面的法线向量

            // 计算点到平面的距离
            float distance = Mathf.Abs(Vector3.Dot(planeNormal, (objectPosition - pointOnPlane)));

            // 将距离转换为绝对值
            distance = Mathf.Max(distance - 4, 0);
            if (distance < minBoundaryDistance)
            {
                minBoundaryDistance = distance;
            }
        }

        int restFriendCount = aliveFriends.Count;
        int restEnemyCount = aliveEnemies.Count;
        var nextInfoText =
            $"我方剩余战机: {restFriendCount}\n敌方剩余战机: {restEnemyCount}\n剩余子弹: {area.player.restBulletCount}\n距离边界: {minBoundaryDistance}\n胜利: {winCount}\n失败: {loseCount}\n平局: {drawCount}\n累计奖励: {area.player.GetCumulativeReward()}";

        if (nextInfoText != infoText.text)
        {
            infoText.text = nextInfoText;
        }
    }

    /// <summary>
    /// 更新敌方指示箭头
    /// </summary>
    /// <param name="enemy"></param>
    private void UpdateArrow(AircraftEnemyIndicator indicator, AircraftAgent enemy)
    {
        indicator.gameObject.SetActive(true);
        Transform enemyTransform = enemy.transform;
        Vector3 viewportPoint = MainCamera.WorldToViewportPoint(enemyTransform.position);
        bool behindCamera = viewportPoint.z < 0;
        viewportPoint.z = 0f;

        // Do position calculations
        Vector3 viewportCenter = new Vector3(.5f, .5f, 0f);
        Vector3 fromCenter = viewportPoint - viewportCenter;
        float halfLimit = indicatorLimit / 2f;
        bool showArrow = false;

        if (behindCamera)
        {
            // Limit distance from center
            // (Viewport point is flipped when object is behind camera)
            fromCenter = -fromCenter.normalized * halfLimit;
            showArrow = true;
        }
        else
        {
            if (fromCenter.magnitude > halfLimit)
            {
                // Limit distance from center
                fromCenter = fromCenter.normalized * halfLimit;
                showArrow = true;
            }
        }

        // Update the checkpoint icon and arrow
        indicator.ToggleArrowActive(showArrow);
        indicator.circle.rectTransform.rotation = Quaternion.FromToRotation(Vector3.up, fromCenter);
        indicator.circle.rectTransform.position =
            MainCamera.ViewportToScreenPoint(fromCenter + viewportCenter);
    }

    /// <summary>
    /// 重新加载当前场景
    /// </summary>
    public void OnResumeButtonClicked()
    {
        GameManager.Instance.OnResumeButtonClicked();
    }

    /// <summary>
    /// 回到主菜单节目
    /// </summary>
    public void OnMainMenuButtonClicked()
    {
        GameManager.Instance.LoadScene("MainMenu", GameState.MainMenu);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChange -= OnStateChange;
        }
    }
}