using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

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
        "Capsule-b-1","Capsule-b-2","Capsule-b-3",
        "HumanM_Model"
    };

    [Header("First Person Camera")]
    public bool setupFirstPersonCamera = true;
    public bool disableMainCamera = true;

    [Header("Ball Pass Setup")]
    public bool setupBallPass = true;
    public BallPassController.PassTeam defaultPassTeam = BallPassController.PassTeam.White;

    [Header("Rendering Settings")]
    [Tooltip("リフレクションプローブ設定を一括調整する")]
    public bool adjustReflectionProbes = true;
    [Tooltip("Reflection Probes の使用モード（Off または Simple 推奨）")]
    public ReflectionProbeUsage reflectionProbeUsage = ReflectionProbeUsage.Off;
    [Tooltip("Anchor Override として使う Transform（任意）")]
    public Transform reflectionProbeAnchorOverride;

    private void Start()
    {
        EnsureCourtManager();
        AttachAgents();
        if (setupFirstPersonCamera)
        {
            SetupFirstPersonCamera();
        }
        if (setupBallPass)
        {
            SetupBallPass();
        }
        if (adjustReflectionProbes)
        {
            ApplyReflectionProbeSettings();
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

            // HumanM_Modelの場合は特別な処理
            if (name == "HumanM_Model")
            {
                var walker = obj.GetComponent<HumanMWalker>();
                if (walker == null)
                {
                    walker = obj.AddComponent<HumanMWalker>();
                }
            }
            else
            {
                var controller = obj.GetComponent<BasketballAgentController>();
                if (controller == null)
                {
                    controller = obj.AddComponent<BasketballAgentController>();
                }
            }

            // CharacterController を付与 (存在しない場合)
            var cc = obj.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = obj.AddComponent<CharacterController>();

                // HumanM_Model用の特別な設定
                if (name == "HumanM_Model")
                {
                    // 人間キャラクター用の設定
                    cc.center = Vector3.up * 0.9f;
                    cc.height = 1.8f;
                    cc.radius = 0.3f;
                }
                else
                {
                    // Capsule のサイズに合わせる簡易設定
                    cc.center = Vector3.up * 1.0f;
                    cc.height = 2.0f;
                    cc.radius = 0.4f;
                }
            }

            // 物理衝突を避けるために Rigidbody は不要。重力も使わない前提
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // HumanM_Model用のアニメーション設定
            if (name == "HumanM_Model")
            {
                SetupHumanMAnimation(obj);
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

    private void SetupBallPass()
    {
        // ball を探してコントローラをアタッチ
        GameObject ball = GameObject.Find("ball");
        if (ball == null)
        {
            Debug.LogWarning("AutoSetup: ball が見つかりませんでした。");
            return;
        }

        BallPassController ctrl = ball.GetComponent<BallPassController>();
        if (ctrl == null)
        {
            ctrl = ball.AddComponent<BallPassController>();
        }
        ctrl.passTeam = defaultPassTeam;
    }

    private void ApplyReflectionProbeSettings()
    {
        // 対象: ball と 各エージェント（子も含む）
        var targets = agentNames
            .Select(GameObject.Find)
            .Where(go => go != null)
            .ToList();

        var ball = GameObject.Find("ball");
        if (ball != null) targets.Add(ball);

        foreach (var root in targets)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                mr.reflectionProbeUsage = reflectionProbeUsage;
                if (reflectionProbeAnchorOverride != null)
                {
                    mr.probeAnchor = reflectionProbeAnchorOverride;
                }
            }
        }
    }

    private void SetupHumanMAnimation(GameObject humanModel)
    {
        // Animationコンポーネントは追加しない（Legacyアニメーションエラー回避のため）
        // Animation animationComponent = humanModel.GetComponent<Animation>();
        // if (animationComponent == null)
        // {
        //     animationComponent = humanModel.AddComponent<Animation>();
        //     Debug.Log("AutoSetup: Animationコンポーネントを追加しました（ジャンプアニメーション用）");
        // }

        // Animatorコンポーネントを追加
        Animator animator = humanModel.GetComponent<Animator>();
        if (animator == null)
        {
            animator = humanModel.AddComponent<Animator>();
        }

        // NavMeshAgentコンポーネントを追加
        UnityEngine.AI.NavMeshAgent navMeshAgent = humanModel.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = humanModel.AddComponent<UnityEngine.AI.NavMeshAgent>();
            Debug.Log("AutoSetup: NavMeshAgentコンポーネントを追加しました");
        }

        // HumanMWalkerコンポーネントを取得
        HumanMWalker walker = humanModel.GetComponent<HumanMWalker>();

        // 歩行アニメーションコントローラーを探して設定
        RuntimeAnimatorController walkController = FindWalkAnimatorController();

        if (walkController != null)
        {
            animator.runtimeAnimatorController = walkController;

            // HumanMWalkerにも設定
            if (walker != null)
            {
                walker.SetAnimatorController(walkController);
            }

            Debug.Log($"AutoSetup: HumanM_Modelに歩行アニメーションを設定しました: {walkController.name}");
        }
        else
        {
            Debug.LogWarning("AutoSetup: 歩行アニメーションコントローラーが見つかりませんでした。手動で設定してください。");
        }

        // アニメーション速度を調整（必要に応じて）
        animator.speed = 1.0f;
    }

    private RuntimeAnimatorController FindWalkAnimatorController()
    {
        // 複数の歩行アニメーションコントローラーを試す
        string[] controllerNames = {
            "HumanM@Walk01_Forward",
            "HumanM@MaskedWalking",
            "HumanM@Walk01_ForwardRight",
            "HumanM@Walk01_ForwardLeft"
        };

        foreach (string controllerName in controllerNames)
        {
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(controllerName);
            if (controller != null)
            {
                Debug.Log($"AutoSetup: アニメーションコントローラーを発見: {controllerName}");
                return controller;
            }
        }

        // Resourcesフォルダから見つからない場合は、シーン内のオブジェクトから検索
        Debug.LogWarning("AutoSetup: Resourcesフォルダからアニメーションコントローラーが見つかりませんでした。");
        Debug.LogWarning("AutoSetup: 手動でアニメーションコントローラーを設定してください。");

        return null;
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


