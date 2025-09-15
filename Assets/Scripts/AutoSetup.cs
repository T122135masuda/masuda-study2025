using System.Linq;
using UnityEngine;

public class AutoSetup : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureAutoSetupExists()
    {
        if (FindObjectOfType<AutoSetup>() != null) return;
        var go = new GameObject("AutoSetup");
        go.AddComponent<AutoSetup>();
        Object.DontDestroyOnLoad(go);
    }

    [Tooltip("Capsule オブジェクト名の接頭辞。該当名を持つオブジェクトを自動検出します。")]
    public string[] agentNames =
    {
        "Capsule-w-1","Capsule-w-2","Capsule-w-3",
        "Capsule-b-1","Capsule-b-2","Capsule-b-3"
    };

    [Header("First Person Camera")]
    public bool setupFirstPersonCamera = true;
    public bool disableMainCamera = true;

    private void Start()
    {
        EnsureCourtManager();
        AttachAgents();
        if (setupFirstPersonCamera)
        {
            SetupFirstPersonCamera();
        }
    }

    private void EnsureCourtManager()
    {
        if (FindObjectOfType<CourtManager>() == null)
        {
            var go = new GameObject("CourtManager");
            go.AddComponent<CourtManager>();
        }
    }

    private void AttachAgents()
    {
        foreach (var name in agentNames)
        {
            var obj = GameObject.Find(name);
            if (obj == null)
            {
                Debug.LogWarning($"AutoSetup: {name} が見つかりません");
                continue;
            }

            var controller = obj.GetComponent<BasketballAgentController>();
            if (controller == null)
            {
                controller = obj.AddComponent<BasketballAgentController>();
            }

            // CharacterController を付与 (存在しない場合)
            var cc = obj.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = obj.AddComponent<CharacterController>();
                // Capsule のサイズに合わせる簡易設定
                cc.center = Vector3.up * 1.0f;
                cc.height = 2.0f;
                cc.radius = 0.4f;
            }

            // 物理衝突を避けるために Rigidbody は不要。重力も使わない前提
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }

    private void SetupFirstPersonCamera()
    {
        if (!setupFirstPersonCamera) return;
        
        // head-cameraオブジェクトを検索
        GameObject headCamera = GameObject.Find("head-camera");
        if (headCamera == null)
        {
            Debug.LogWarning("AutoSetup: head-cameraが見つかりませんでした。");
            return;
        }
        
        Debug.Log($"AutoSetup: head-cameraを検出しました - 位置: {headCamera.transform.position}, ローカル位置: {headCamera.transform.localPosition}");
        
        // head-cameraにFirstPersonCameraをアタッチ
        FirstPersonCamera firstPersonCamera = headCamera.GetComponent<FirstPersonCamera>();
        if (firstPersonCamera == null)
        {
            firstPersonCamera = headCamera.AddComponent<FirstPersonCamera>();
        }
        
        // カメラコンポーネントを追加
        Camera camera = headCamera.GetComponent<Camera>();
        if (camera == null)
        {
            camera = headCamera.AddComponent<Camera>();
        }
        
        Debug.Log("AutoSetup: head-cameraにFirstPersonCameraをアタッチしました: " + headCamera.name);
        
        // メインカメラを無効化（オプション）
        if (disableMainCamera)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.gameObject != headCamera)
            {
                mainCamera.enabled = false;
                Debug.Log("AutoSetup: メインカメラを無効化しました");
            }
        }
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }
            
            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
}


