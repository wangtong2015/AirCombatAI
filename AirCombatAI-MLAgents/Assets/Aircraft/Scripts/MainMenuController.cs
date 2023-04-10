using System.Collections;
using System.Collections.Generic;
using Aircraft;
using TMPro;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{

    [Tooltip("场景列表")]
    public List<string> scenes;
    [Tooltip("场景下拉菜单")]
    public TMP_Dropdown scenesDropdown;
    
    private string selectedScene;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Assert(scenes.Count > 0, "没有设置场景");
        scenesDropdown.ClearOptions();
        scenesDropdown.AddOptions(scenes);
        selectedScene = scenes[0];
    }
    
    public void SetScene(int sceneIndex)
    {
        selectedScene = scenes[sceneIndex];
    }
    
    public void StartButtonClicked()
    {
        GameManager.Instance.LoadScene(selectedScene, GameState.Playing);
    }
    
    /// <summary>
    /// Quit the game
    /// </summary>
    public void QuitButtonClicked()
    {
        Application.Quit();
    }
}
