using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Usingを追加
using UnityEngine.SceneManagement;

public class SceneChange : MonoBehaviour
{
    private Coroutine sceneChangeCoroutine;
    [SerializeField] private float delayTime = 0.5f;
    [SerializeField] private string targetSceneName = "SampleScene";
    [SerializeField] private string secondarySceneName = "SampleScene1";
    [SerializeField] private string thirdSceneName = "SampleScene2";

    void Start()
    {
        LogWithOverride($"SceneChange: Ready. Primary button loads '{targetSceneName}', secondary loads '{secondarySceneName}', third loads '{thirdSceneName}' after {delayTime} seconds.");
    }

    /// <summary>
    /// UIボタンのOnClickイベントから呼び出してシーン遷移を開始する
    /// </summary>
    public void OnSceneChangeButtonPressed()
    {
        BeginSceneChange(targetSceneName);
    }

    /// <summary>
    /// 2つ目のボタン。 secondarySceneName をロードする。
    /// </summary>
    public void OnSecondarySceneButtonPressed()
    {
        BeginSceneChange(secondarySceneName);
    }

    /// <summary>
    /// 3つ目のボタン。 thirdSceneName をロードする。
    /// </summary>
    public void OnThirdSceneButtonPressed()
    {
        BeginSceneChange(thirdSceneName);
    }

    private void BeginSceneChange(string sceneName)
    {
        LogWithOverride($"SceneChange: Button pressed. Preparing to load '{sceneName}' after {delayTime} seconds.");
        if (sceneChangeCoroutine != null)
        {
            StopCoroutine(sceneChangeCoroutine);
        }
        sceneChangeCoroutine = StartCoroutine(ChangeSceneAfterDelay(delayTime, sceneName));
    }

    private IEnumerator ChangeSceneAfterDelay(float delay, string sceneName)
    {
        // AutoSetupがデバッグログを無効化している可能性があるため、一時的に有効化
        bool originalLogState = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = true;

        Debug.Log($"SceneChange: Waiting {delay} seconds before loading '{sceneName}'...");
        //指定した秒数待つ
        yield return new WaitForSeconds(delay);

        Debug.Log($"SceneChange: Delay finished. Attempting to load scene '{sceneName}'");

        // シーンが存在するか確認（ビルド設定から検索）
        bool sceneExists = false;
        int sceneIndex = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameInBuild == sceneName)
            {
                sceneExists = true;
                sceneIndex = i;
                Debug.Log($"SceneChange: Found scene '{sceneName}' at index {i}");
                break;
            }
        }

        if (!sceneExists)
        {
            Debug.LogError($"SceneChange: Scene '{sceneName}' not found in build settings!");
            Debug.Log($"SceneChange: Available scenes in build settings:");
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                Debug.Log($"SceneChange:   - Index {i}: {sceneNameInBuild}");
            }
            Debug.unityLogger.logEnabled = originalLogState;
            yield break;
        }

        //NewSceneを呼び出す
        Debug.Log($"SceneChange: Loading scene '{sceneName}' (index {sceneIndex})...");
        Debug.unityLogger.logEnabled = originalLogState;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        bool originalLogState = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = true;
        Debug.Log("SceneChange: OnDestroy called");
        Debug.unityLogger.logEnabled = originalLogState;

        //Coroutineをキャンセルする（念のため）
        if (sceneChangeCoroutine != null)
        {
            StopCoroutine(sceneChangeCoroutine);
        }
    }

    private void LogWithOverride(string message)
    {
        bool originalLogState = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = true;
        Debug.Log(message);
        Debug.unityLogger.logEnabled = originalLogState;
    }
}
