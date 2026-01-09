using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

public class HumanMWalker : MonoBehaviour
{
    [Header("Walking Settings")]
    [Tooltip("歩行速度（m/s）")]
    public float walkSpeed = 2.0f;

    [Tooltip("開始位置")]
    public Vector3 startPosition = new Vector3(4.99f, 0.978f, 4.45f); // 開始位置

    [Tooltip("経由点")]
    public Vector3 waypoint = new Vector3(0.05f, 0f, 3.71f); // 経由点

    [Tooltip("最終目標位置")]
    public Vector3 targetPosition = new Vector3(-10.77f, 0f, 3.71f); // 最終目標

    [Tooltip("テレポート先位置（15秒後に移動）")]
    public Vector3 teleportPosition = new Vector3(2.1f, 0.978f, 4.45f); // テレポート先位置

    [Tooltip("テレポートまでの待機時間（秒）")]
    public float teleportDelayTime = 15.0f; // 15秒後にテレポート

    [Tooltip("テレポート後の目標位置（この位置まで歩行）")]
    public Vector3 walkTargetPosition = new Vector3(-2.17f, 0.978f, 4.45f); // 目標位置

    [Tooltip("到着判定の距離（m）")]
    public float arrivalDistance = 0.5f;

    [Tooltip("到着後の待機時間（秒）")]
    public float waitTime = 2.0f;

    [Tooltip("特定位置での待機時間（秒）")]
    public float specialWaitTime = 3.0f;

    [Tooltip("エンターキー押下後の待機時間（秒）")]
    public float startDelayTime = 15.0f;

    [Tooltip("自動開始")]
    public bool autoStart = false;

    [Header("Animation")]
    [Tooltip("アニメーション速度の調整")]
    public float animationSpeed = 1.0f;

    [Tooltip("歩行アニメーションコントローラー")]
    public RuntimeAnimatorController walkAnimatorController;

    // ジャンプアニメーションは廃止

    [Tooltip("歩行アニメーションクリップ")]
    public AnimationClip walkAnimationClip;

    // ジャンプアニメーションは廃止

    [Tooltip("アニメーションのブレンド時間")]
    public float animationBlendTime = 0.2f;

    [Header("Collision Avoidance")]
    [Tooltip("他のオブジェクトとの衝突を避ける")]
    public bool enableCollisionAvoidance = true;

    [Tooltip("衝突回避の検出距離")]
    public float avoidanceDistance = 1.0f;

    [Tooltip("衝突回避の力の強さ")]
    public float avoidanceForce = 2.0f;

    // ジャンプ関連設定は廃止

    [Header("Debug")]
    [Tooltip("デバッグ情報を表示")]
    public bool showDebugInfo = true;

    [Header("Position Recording")]
    [Tooltip("座標記録を有効にする")]
    public bool enablePositionRecording = true;
    [Tooltip("座標データのファイル名")]
    public string positionDataFileName = "HumanMPositionData";

    private Vector3 _currentTarget;
    private bool _isMoving = false;
    private bool _isWaiting = false;
    private float _waitTimer = 0f;
    private bool _isDelayedStart = false;
    private float _delayTimer = 0f;
    // ジャンプ関連の内部状態は廃止
    private Animator _animator;
    private CharacterController _characterController;

    // テレポートシーケンス用の変数
    private Coroutine _teleportSequenceCoroutine = null;
    private float _sequenceStartTime = -1f;
    private bool _isTeleportWalk = false; // テレポート後の歩行アニメだけ行う区間

    // Legacy アニメーションは未使用

    // 移動段階の管理
    // 段階管理は単純化（ゴールへ歩行のみ）

    // アニメーショントリガー
    private static readonly int WalkTriggerHash = Animator.StringToHash("Walk");
    private static readonly int IdleTriggerHash = Animator.StringToHash("Idle");

    private float _recordingStartTime = -1f;
    private bool _isRecording = false;
    private const float RECORDING_DURATION = 180.0f; // 3分間記録
    [Range(30, 120)]
    public int targetRecordingFrequency = 20; // 目標記録周波数（Hz）- 元のCSVと同じ20Hz
    private float _recordInterval;
    private float _nextRecordTime; // 次の記録時刻
    private string _dataFolderPath;

    // Cube-human専用の座標記録用
    private List<CubeHumanPositionData> _cubeHumanPositionList = new List<CubeHumanPositionData>();
    private bool _isCubeHumanRecording = false;
    private float _cubeHumanRecordingStartTime = -1f;
    private float _cubeHumanNextRecordTime = 0f; // Cube-human用の次の記録時刻

    // Cube-human座標データ構造
    [System.Serializable]
    public struct CubeHumanPositionData
    {
        public float timestamp; // 記録開始からの相対時間（秒）
        public float positionX;
        public float positionY;
        public float positionZ;
    }

    private void Start()
    {
        // コンポーネントの取得
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
        // Legacy Animation は未使用

        // 初期位置を確実に設定（Y座標を0.978に統一）
        Vector3 initPos = startPosition;
        initPos.y = 0.978f;
        transform.position = initPos;
        _isMoving = false; // 初期状態では移動しない（エンターキー待ち）

        // 座標記録用の初期化
        _dataFolderPath = @"C:\Users\vrdsl\Desktop\masuda-lab\masuda-study2025\Assets\data";
        if (!Directory.Exists(_dataFolderPath))
        {
            Directory.CreateDirectory(_dataFolderPath);
            if (showDebugInfo)
            {
                Debug.Log($"[HumanMWalker] データフォルダを作成しました: {_dataFolderPath}");
            }
        }
        SharedPositionDataRecorder.Clear();
        _isRecording = false;
        _recordingStartTime = -1f;
        // 記録間隔を計算（秒）
        _recordInterval = 1.0f / targetRecordingFrequency;

        // Cube-humanの場合は自動的に座標記録を開始
        if (gameObject.name == "Cube-human")
        {
            _cubeHumanPositionList.Clear();
            _isCubeHumanRecording = true;
            _cubeHumanRecordingStartTime = Time.time;
            _cubeHumanNextRecordTime = Time.time + _recordInterval; // 最初の記録時刻を設定
            if (showDebugInfo)
            {
                Debug.Log($"[HumanMWalker] Cube-humanの座標記録を開始しました（{targetRecordingFrequency}Hz）");
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"HumanMWalker: 初期位置を設定しました - {startPosition}");
            Debug.Log($"HumanMWalker: 実際の位置 - {transform.position}");
            Debug.Log($"HumanMWalker: 初期目標 - {_currentTarget}");
            Debug.Log($"HumanMWalker: 移動状態 - {_isMoving}");
        }

        // アニメーション設定
        SetupAnimation();

        // CharacterController設定
        SetupCharacterController();

        // 初期状態はIdleアニメーション
        UpdateAnimation(false, 0f);

        // テレポートシーケンスを開始
        _sequenceStartTime = Time.time;
        _teleportSequenceCoroutine = StartCoroutine(TeleportSequence());

        // 自動開始
        if (autoStart)
        {
            StartWalking();
        }
    }

    private void SetupAnimation()
    {
        if (_animator == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: Animatorコンポーネントが見つかりません。アニメーションを設定できません。");
            }
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"HumanMWalker: Animator設定開始 - 現在のController: {(_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "None")}");
        }

        // アニメーション速度を設定
        _animator.speed = animationSpeed;

        // アニメーションクリップを自動検索
        if (walkAnimationClip == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: 歩行アニメーションクリップを検索中...");
            }

            // 複数の歩行アニメーションを試す
            string[] walkClipNames = {
                "HumanM@Walk01_Forward",
                "HumanM@Walk01_ForwardRight",
                "HumanM@Walk01_Right",
                "HumanM@Walk01_Left"
            };

            foreach (string clipName in walkClipNames)
            {
                walkAnimationClip = Resources.Load<AnimationClip>(clipName);
                if (walkAnimationClip != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: 歩行アニメーションクリップを発見: {clipName}");
                    }
                    break;
                }
            }

            // Resourcesフォルダから見つからない場合は、シーン内のアセットから検索
            if (walkAnimationClip == null)
            {
                if (showDebugInfo)
                {
                    Debug.Log("HumanMWalker: シーン内のアセットから歩行アニメーションを検索中...");
                }
                walkAnimationClip = FindWalkAnimationClipInScene();
            }
        }

        // ジャンプアニメーションの自動検索は廃止

        // アニメーションコントローラーが設定されていない場合は自動検索
        if (walkAnimatorController == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: アニメーションコントローラーを自動検索中...");
            }

            // Resourcesフォルダから検索
            walkAnimatorController = Resources.Load<RuntimeAnimatorController>("HumanM@Walk01_Forward");

            // 見つからない場合は他の歩行アニメーションを試す
            if (walkAnimatorController == null)
            {
                walkAnimatorController = Resources.Load<RuntimeAnimatorController>("HumanM@MaskedWalking");
            }

            if (walkAnimatorController == null)
            {
                walkAnimatorController = Resources.Load<RuntimeAnimatorController>("HumanM@Walk01_ForwardRight");
            }
        }

        // アニメーションコントローラーを設定
        if (walkAnimatorController != null)
        {
            _animator.runtimeAnimatorController = walkAnimatorController;
            _animator.enabled = true; // Animatorを有効化

            if (showDebugInfo)
            {
                Debug.Log($"HumanMWalker: アニメーションコントローラーを設定しました: {walkAnimatorController.name}");
                Debug.Log($"HumanMWalker: Animator有効状態: {_animator.enabled}");
                Debug.Log($"HumanMWalker: Animator速度: {_animator.speed}");
            }
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: 歩行アニメーションコントローラーが見つかりませんでした。手動で設定してください。");
            }
        }
    }

    private void SetupCharacterController()
    {
        if (_characterController == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: CharacterControllerコンポーネントが見つかりません。Transform移動を使用します。");
            }
            return;
        }

        // CharacterControllerの基本設定
        _characterController.enabled = true;

        if (showDebugInfo)
        {
            Debug.Log("HumanMWalker: CharacterControllerで移動します。");
        }
    }

    private void Update()
    {
        // エンターキーで歩行開始
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (showDebugInfo)
            {
                Debug.Log("=== エンターキーが押されました ===");
                Debug.Log($"スタート位置: {startPosition}");
            }

            // 座標記録を開始
            if (enablePositionRecording)
            {
                StartPositionRecording();
            }

            // 共有タイムスタンプを設定（BallPassControllerと同期）
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            SharedPositionDataRecorder.SetSharedTimestamp(timestamp);

            // エンターキーを押した場合は、指定秒数待機
            _isDelayedStart = true;
            _delayTimer = startDelayTime;
            _isMoving = false;
            _isWaiting = false;

            // 強制的に初期位置に設定（Y座標を0.978に統一）
            Vector3 resetPos = startPosition;
            resetPos.y = 0.978f;
            transform.position = resetPos;
            if (_characterController != null)
            {
                _characterController.enabled = false;
                _characterController.transform.position = resetPos;
                _characterController.enabled = true;
            }

            if (showDebugInfo)
            {
                Debug.Log($"HumanMWalker: エンターキー押下。{startDelayTime}秒後に歩行開始します。");
                Debug.Log($"HumanMWalker: 設定位置: {startPosition}");
                Debug.Log($"HumanMWalker: 実際の位置: {transform.position}");
            }
        }

        // 待機中の処理
        if (_isDelayedStart)
        {
            _delayTimer -= Time.deltaTime;
            if (_delayTimer <= 0f)
            {
                // 待機完了。経由点へは歩行しない
                _isDelayedStart = false;
                _isMoving = false;
                _isWaiting = false;
                UpdateAnimation(false, 0f);

                if (showDebugInfo)
                {
                    Debug.Log("=== 待機完了（経由点へは移動しません） ===");
                }
            }
            else
            {
                // 待機中はIdleアニメーション
                UpdateAnimation(false, 0f);

                // 待機中のデバッグ情報
                if (showDebugInfo && Time.frameCount % 60 == 0) // 1秒に1回
                {
                    Debug.Log($"HumanMWalker: 待機中 - 残り時間: {_delayTimer:F1}秒");
                }
            }
        }

        if (_isMoving)
        {
            MoveToTarget();
            UpdateAnimation(true, walkSpeed);
        }
        else if (_isWaiting)
        {
            HandleWaiting();
            UpdateAnimation(false, 0f);
        }
        else
        {
            // 移動も待機もしていない場合の処理
            UpdateAnimation(false, 0f);
            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"HumanMWalker: 待機状態 - 移動中: {_isMoving}, 待機中: {_isWaiting}");
            }
        }

        // 座標記録（エンターキー押下から3分間、指定された周波数でデータを収集）
        if (_isRecording && enablePositionRecording && Time.time >= _nextRecordTime)
        {
            RecordPosition();
            _nextRecordTime = Time.time + _recordInterval;
            CheckRecordingDuration();
        }

        // Cube-humanの座標記録（シーン開始時から自動記録）
        if (_isCubeHumanRecording && enablePositionRecording)
        {
            if (_cubeHumanNextRecordTime == 0f)
            {
                _cubeHumanNextRecordTime = Time.time + _recordInterval;
            }

            if (Time.time >= _cubeHumanNextRecordTime)
            {
                RecordCubeHumanPosition();
                _cubeHumanNextRecordTime = Time.time + _recordInterval;
            }
        }
    }

    private void MoveToTarget()
    {
        // 目標までの距離を計算（XZ平面のみ）
        Vector3 currentPos = transform.position;
        Vector3 targetPos = _currentTarget;
        currentPos.y = 0f; // Y軸を無視
        targetPos.y = 0f;  // Y軸を無視
        float distanceToTarget = Vector3.Distance(currentPos, targetPos);

        // 到着判定
        if (distanceToTarget <= arrivalDistance)
        {
            if (showDebugInfo)
            {
                Debug.Log($"HumanMWalker: 目標に到着。現在位置: {transform.position}, 目標: {_currentTarget}, 距離: {distanceToTarget:F2}");
            }
            ArrivedAtTarget();
            return;
        }

        // 移動方向を計算
        Vector3 direction = (_currentTarget - transform.position).normalized;
        direction.y = 0f; // Y軸の移動は無効化

        // 衝突回避の処理
        if (enableCollisionAvoidance)
        {
            Vector3 avoidanceDirection = ComputeAvoidanceDirection();
            if (avoidanceDirection.magnitude > 0.1f)
            {
                // 衝突回避の方向を優先
                direction = avoidanceDirection.normalized;
                if (showDebugInfo && Time.frameCount % 120 == 0)
                {
                    Debug.Log($"HumanMWalker: 衝突回避中 - 回避方向: {avoidanceDirection}");
                }
            }
        }

        // 移動量を計算
        Vector3 movement = direction * walkSpeed * Time.deltaTime;

        // CharacterControllerを使用した移動
        if (_characterController != null && _characterController.enabled)
        {
            // CharacterControllerのMoveは相対移動
            _characterController.Move(movement);
            // Y座標を0.978に維持
            Vector3 pos = transform.position;
            pos.y = 0.978f;
            transform.position = pos;
        }
        else
        {
            // CharacterControllerがない場合は直接位置変更
            transform.position += movement;
            // Y座標を0.978に維持
            Vector3 pos = transform.position;
            pos.y = 0.978f;
            transform.position = pos;
        }

        // 回転（移動方向を向く）
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 5f * Time.deltaTime);
        }

        // デバッグ情報（一定間隔で）
        if (showDebugInfo && Time.frameCount % 60 == 0) // 1秒に1回
        {
            Debug.Log($"HumanMWalker: 移動中 - 現在位置: {transform.position}, 目標: {_currentTarget}, 距離: {distanceToTarget:F2}");
            Debug.Log($"HumanMWalker: 移動方向: {direction}, 移動量: {movement}");
            Debug.Log($"HumanMWalker: 移動状態フラグ - _isMoving:{_isMoving}, _isWaiting:{_isWaiting}");
        }
    }


    private void ArrivedAtTarget()
    {
        _isMoving = false;

        if (showDebugInfo)
        {
            Debug.Log("HumanMWalker: 目標に到着しました。移動完了。");
        }
    }

    private void HandleWaiting()
    {
        // 通常の待機タイマーのみ
        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0f)
        {
            _isWaiting = false;

            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: 待機完了");
            }
        }
    }

    public void StartWalking()
    {
        // 強制的に初期位置に設定（Y座標を0.978に統一）
        Vector3 resetPos = startPosition;
        resetPos.y = 0.978f;
        transform.position = resetPos;
        if (_characterController != null)
        {
            _characterController.enabled = false;
            _characterController.transform.position = resetPos;
            _characterController.enabled = true;
        }

        _isMoving = false;
        _isWaiting = false;

        // Idleアニメーション
        UpdateAnimation(false, 0f);

        if (showDebugInfo)
        {
            Debug.Log("HumanMWalker: 初期位置にリセットしました。");
            Debug.Log($"HumanMWalker: 設定位置: {startPosition}");
            Debug.Log($"HumanMWalker: 実際の位置: {transform.position}");
        }
    }

    public void StopWalking()
    {
        _isMoving = false;
        _isWaiting = false;
        Debug.Log("HumanMWalker: 歩行停止");
    }

    public void SetTargetPosition(Vector3 newTarget)
    {
        targetPosition = newTarget;
        if (!_isMoving && !_isWaiting)
        {
            _currentTarget = targetPosition;
        }
    }

    public void SetStartPosition(Vector3 newStart)
    {
        startPosition = newStart;
        startPosition.y = 0.978f; // Y座標を0.978に統一
        if (!_isMoving && !_isWaiting)
        {
            Vector3 pos = startPosition;
            pos.y = 0.978f;
            transform.position = pos;
        }
    }

    public bool IsMoving()
    {
        return _isMoving;
    }

    public Vector3 GetCurrentTarget()
    {
        return _currentTarget;
    }

    private void UpdateAnimation(bool isWalking, float speed)
    {
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"HumanMWalker: UpdateAnimation呼び出し - 歩行:{isWalking}, 速度:{speed:F2}");
        }

        if (_animator == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: Animatorがnullです。アニメーションを更新できません。");
            }
            return;
        }

        // Animatorが無効の場合は有効化
        if (!_animator.enabled)
        {
            _animator.enabled = true;
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: Animatorを有効化しました");
            }
        }

        // Animator Controllerが設定されているかチェック
        if (_animator.runtimeAnimatorController == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: Animator Controllerが設定されていません。直接アニメーションクリップを再生します。");
            }
        }

        // アニメーション状態に応じてアニメーションを再生
        if (isWalking && speed > 0f)
        {
            // 歩行アニメーション
            if (walkAnimationClip != null)
            {
                _animator.Play(walkAnimationClip.name);
                _animator.speed = animationSpeed * (speed / 2.0f); // 基準速度2.0fで正規化

                if (showDebugInfo)
                {
                    Debug.Log($"HumanMWalker: 歩行アニメーション再生: {walkAnimationClip.name}");
                }
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.Log("HumanMWalker: 歩行アニメーションクリップが設定されていません");
                }
            }
        }
        else
        {
            // 待機アニメーション（デフォルトのIdle状態）
            _animator.speed = animationSpeed;

            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: 待機アニメーション（デフォルト状態）");
            }
        }

        // デバッグ情報を表示（一定間隔で）
        if (showDebugInfo && Time.frameCount % 60 == 0) // 1秒に1回
        {
            Debug.Log($"HumanMWalker: アニメーション状態 - 歩行中:{isWalking}, 速度:{speed:F2}, Animator速度:{_animator.speed:F2}");
            Debug.Log($"HumanMWalker: Animator Controller: {(_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "None")}");
            Debug.Log($"HumanMWalker: 歩行アニメーションクリップ: {(walkAnimationClip != null ? walkAnimationClip.name : "None")}");
        }
    }

    // 外部からアニメーションコントローラーを設定するAPI
    public void SetAnimatorController(RuntimeAnimatorController controller)
    {
        walkAnimatorController = controller;
        if (_animator != null)
        {
            _animator.runtimeAnimatorController = controller;
            Debug.Log($"HumanMWalker: アニメーションコントローラーを変更しました: {controller.name}");
        }
    }

    // アニメーション速度を設定するAPI
    public void SetAnimationSpeed(float speed)
    {
        animationSpeed = speed;
        if (_animator != null)
        {
            _animator.speed = animationSpeed;
        }
    }

    // Animatorパラメータの存在チェック（使用しない）
    // private bool HasParameter(string paramName, Animator animator)
    // {
    //     if (animator == null || animator.runtimeAnimatorController == null)
    //         return false;
    //
    //     foreach (AnimatorControllerParameter param in animator.parameters)
    //     {
    //         if (param.name == paramName)
    //             return true;
    //     }
    //     return false;
    // }


    private AnimationClip FindWalkAnimationClipInScene()
    {
        // シーン内のすべてのアニメーションクリップを検索
        AnimationClip[] allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();

        if (showDebugInfo)
        {
            Debug.Log($"HumanMWalker: シーン内で{allClips.Length}個のアニメーションクリップを検索中...");
        }

        string[] walkKeywords = { "Walk", "walk", "WALK" };

        foreach (AnimationClip clip in allClips)
        {
            if (clip == null) continue;

            string clipName = clip.name;

            // 歩行関連のキーワードを含むクリップを検索
            foreach (string keyword in walkKeywords)
            {
                if (clipName.Contains(keyword))
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: 歩行アニメーションクリップを発見: {clipName}");
                    }
                    return clip;
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log("HumanMWalker: シーン内で歩行アニメーションクリップが見つかりませんでした");
            Debug.Log("HumanMWalker: 利用可能なアニメーションクリップ:");
            foreach (AnimationClip clip in allClips)
            {
                if (clip != null)
                {
                    Debug.Log($"  - {clip.name}");
                }
            }
        }

        return null;
    }

    // ジャンプアニメーション探索は削除

    private Vector3 ComputeAvoidanceDirection()
    {
        Vector3 avoidanceDirection = Vector3.zero;

        // 周囲のオブジェクトを検出
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, avoidanceDistance);

        foreach (Collider col in nearbyColliders)
        {
            // 自分自身は除外
            if (col.gameObject == gameObject) continue;

            // カプセルオブジェクトを検出
            if (col.name.Contains("Capsule"))
            {
                Vector3 directionAway = (transform.position - col.transform.position).normalized;
                directionAway.y = 0f; // Y軸は無視

                float distance = Vector3.Distance(transform.position, col.transform.position);
                float avoidanceStrength = 1.0f - (distance / avoidanceDistance);
                avoidanceStrength = Mathf.Clamp01(avoidanceStrength);

                avoidanceDirection += directionAway * avoidanceStrength * avoidanceForce;

                if (showDebugInfo && Time.frameCount % 120 == 0)
                {
                    Debug.Log($"HumanMWalker: カプセル検出 - {col.name}, 距離: {distance:F2}, 回避強度: {avoidanceStrength:F2}");
                }
            }
        }

        return avoidanceDirection;
    }

    // デバッグ情報を画面に表示
    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 300, 400, 250));
        GUILayout.Label("=== HumanMWalker Debug Info ===", GUI.skin.box);
        GUILayout.Label($"移動中: {_isMoving}");
        GUILayout.Label($"待機中: {_isWaiting}");
        GUILayout.Label($"開始待機中: {_isDelayedStart}");
        GUILayout.Label($"現在の目標: {_currentTarget}");
        GUILayout.Label($"現在位置: {transform.position}");
        GUILayout.Label($"設定開始位置: {startPosition}");

        // 位置の差を表示
        Vector3 positionDiff = transform.position - startPosition;
        GUILayout.Label($"位置差: {positionDiff}");
        GUILayout.Label($"歩行速度: {walkSpeed:F2} m/s");
        // 特定待機位置は廃止
        if (_isWaiting)
        {
            GUILayout.Label($"待機残り時間: {_waitTimer:F1}秒");
        }
        if (_isDelayedStart)
        {
            GUILayout.Label($"開始待機残り時間: {_delayTimer:F1}秒");
        }

        // 目標までの距離を表示
        float distanceToTarget = Vector3.Distance(transform.position, _currentTarget);
        GUILayout.Label($"目標までの距離: {distanceToTarget:F2}m");

        // 衝突回避情報を表示
        if (enableCollisionAvoidance)
        {
            GUILayout.Space(5);
            GUILayout.Label("衝突回避:", GUI.skin.box);
            GUILayout.Label($"検出距離: {avoidanceDistance:F1}m");
            GUILayout.Label($"回避力: {avoidanceForce:F1}");
        }

        GUILayout.Space(5);
        GUILayout.Label("操作:", GUI.skin.box);
        GUILayout.Label($"エンターキー: {startDelayTime:F0}秒待機後に歩行開始");
        GUILayout.Label("移動手順:");
        GUILayout.Label($"1. エンターキー押下 → {startDelayTime:F0}秒待機");
        GUILayout.Label("2. スタート位置 → ゴール位置へ歩行");

        if (_animator != null)
        {
            GUILayout.Label($"Animator有効: {_animator.enabled}");
            GUILayout.Label($"Animator速度: {_animator.speed:F2}");
            GUILayout.Label($"Controller: {(_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "None")}");

            if (_animator.GetCurrentAnimatorStateInfo(0).IsName("HumanM@Walk01_Forward"))
            {
                GUILayout.Label("現在のアニメーション: 歩行中", GUI.skin.box);
            }
            else
            {
                GUILayout.Label("現在のアニメーション: その他", GUI.skin.box);
            }
        }
        else
        {
            GUILayout.Label("Animator: 見つかりません", GUI.skin.box);
        }

        GUILayout.Space(5);
        GUILayout.Label("Animation: 無効化されています（Legacyアニメーションエラー回避のため）", GUI.skin.box);

        GUILayout.EndArea();
    }

    /// <summary>
    /// 座標記録を開始
    /// </summary>
    private void StartPositionRecording()
    {
        _recordingStartTime = Time.time;
        _nextRecordTime = Time.time + _recordInterval; // 次の記録時刻を初期化（最初の記録を即座に実行）
        _isRecording = true;
        // 最初の記録を即座に実行
        RecordPosition();

        // 共有タイムスタンプを設定
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        SharedPositionDataRecorder.SetSharedTimestamp(timestamp);
        SharedPositionDataRecorder.SetTargetFrequency(targetRecordingFrequency);
        SharedPositionDataRecorder.Clear();

        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] 座標記録を開始しました（{RECORDING_DURATION}秒間、{targetRecordingFrequency}Hzで記録）");
            Debug.Log($"[HumanMWalker] 共有タイムスタンプ: {timestamp}");
        }
    }

    /// <summary>
    /// 現在の座標を記録（共有データ構造に追加）
    /// </summary>
    private void RecordPosition()
    {
        if (_recordingStartTime < 0f) return;

        float timestamp = Time.time - _recordingStartTime;
        SharedPositionDataRecorder.AddHumanMPosition(timestamp, transform.position);
    }

    /// <summary>
    /// 記録時間をチェックして、1分経過したらCSVに保存
    /// </summary>
    private void CheckRecordingDuration()
    {
        if (_recordingStartTime < 0f) return;

        float elapsedTime = Time.time - _recordingStartTime;
        if (elapsedTime >= RECORDING_DURATION)
        {
            StopPositionRecording();
        }
    }

    /// <summary>
    /// 座標記録を停止してCSVに保存
    /// </summary>
    private void StopPositionRecording()
    {
        _isRecording = false;

        // 統合データを保存
        SharedPositionDataRecorder.SaveToCSV(_dataFolderPath, showDebugInfo);

        float recordingDuration = Time.time - _recordingStartTime;
        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] 座標記録を終了しました（記録時間: {recordingDuration:F2}秒）");
        }
    }

    /// <summary>
    /// Cube-humanの現在の座標を記録
    /// </summary>
    private void RecordCubeHumanPosition()
    {
        if (_cubeHumanRecordingStartTime < 0f) return;

        float timestamp = Time.time - _cubeHumanRecordingStartTime;
        Vector3 pos = transform.position;

        CubeHumanPositionData data = new CubeHumanPositionData
        {
            timestamp = timestamp,
            positionX = pos.x,
            positionY = pos.y,
            positionZ = pos.z
        };

        _cubeHumanPositionList.Add(data);
    }


    /// <summary>
    /// Cube-humanの座標データをCSVファイルに保存
    /// </summary>
    private void SaveCubeHumanPositionToCSV()
    {
        if (_cubeHumanPositionList.Count == 0)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[HumanMWalker] Cube-humanの座標データがありません");
            }
            return;
        }

        try
        {
            // フォルダが存在しない場合は再作成
            if (!Directory.Exists(_dataFolderPath))
            {
                Directory.CreateDirectory(_dataFolderPath);
                if (showDebugInfo)
                {
                    Debug.Log($"[HumanMWalker] データフォルダを再作成しました: {_dataFolderPath}");
                }
            }

            // 共有タイムスタンプを取得（なければ新規作成）
            string timestamp = SharedPositionDataRecorder.GetSharedTimestamp();
            if (string.IsNullOrEmpty(timestamp))
            {
                timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            string fileName = $"CubeHumanPositionData_{timestamp}.csv";
            string filePath = Path.Combine(_dataFolderPath, fileName).Replace('/', Path.DirectorySeparatorChar);

            if (showDebugInfo)
            {
                Debug.Log($"[HumanMWalker] Cube-human座標データの保存を開始します... パス: {filePath}");
            }

            StringBuilder csv = new StringBuilder();

            // ヘッダー行
            csv.AppendLine("Timestamp,PositionX,PositionY,PositionZ");

            // データ行
            foreach (var data in _cubeHumanPositionList)
            {
                csv.AppendLine($"{data.timestamp:F6},{data.positionX:F6},{data.positionY:F6},{data.positionZ:F6}");
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            // ファイルが実際に存在するか確認
            if (File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                if (showDebugInfo)
                {
                    Debug.Log($"[HumanMWalker] ✓ Cube-human座標データを保存しました: {filePath} (サイズ: {fileSize} bytes, データ件数: {_cubeHumanPositionList.Count}件)");
                }
            }
            else
            {
                Debug.LogError($"[HumanMWalker] ✗ ファイルの保存に失敗しました: {filePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[HumanMWalker] ✗ Cube-human座標データの保存中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    /// <summary>
    /// Cube-humanの位置データを取得（外部からアクセス可能）
    /// </summary>
    public List<CubeHumanPositionData> GetCubeHumanPositionData()
    {
        if (gameObject.name == "Cube-human")
        {
            return new List<CubeHumanPositionData>(_cubeHumanPositionList);
        }
        return new List<CubeHumanPositionData>();
    }

    /// <summary>
    /// オブジェクトが削除される際にCSVに保存
    /// </summary>
    private void OnDestroy()
    {
        // Cube-humanの場合は最終データを保存
        if (gameObject.name == "Cube-human" && _isCubeHumanRecording && _cubeHumanPositionList.Count > 0)
        {
            SaveCubeHumanPositionToCSV();
            if (showDebugInfo)
            {
                Debug.Log("[HumanMWalker] Cube-humanオブジェクト削除時に最終データを保存しました");
            }
        }
    }

    /// <summary>
    /// テレポートシーケンス: 50-60秒と120-150秒のランダム時間後に各1回ずつテレポート → 目標位置まで歩行 → 元の位置にテレポート → 停止
    /// </summary>
    private IEnumerator TeleportSequence()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] テレポートシーケンス開始 - 初期位置: {startPosition}");
        }

        // 初期位置に設定（Y座標を0.978に統一）
        Vector3 initialPos = startPosition;
        initialPos.y = 0.978f;
        transform.position = initialPos;
        if (_characterController != null)
        {
            _characterController.enabled = false;
            _characterController.transform.position = initialPos;
            _characterController.enabled = true;
        }
        _isMoving = false;
        UpdateAnimation(false, 0f);

        // 1回目: 50秒から60秒の間でランダムに待機
        float firstDelay = UnityEngine.Random.Range(50f, 60f);
        yield return new WaitForSeconds(firstDelay);

        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] 1回目: {firstDelay:F2}秒経過 - テレポート位置へ移動: {teleportPosition}");
        }

        // 1回目のテレポートと歩行を実行
        yield return StartCoroutine(ExecuteTeleportAndWalk());

        // 2回目までの待機時間を計算（120秒から150秒の間でランダム）
        float secondDelay = UnityEngine.Random.Range(120f, 150f);
        // 1回目の待機時間を引いて、残りの待機時間を計算
        float remainingDelay = secondDelay - firstDelay;
        if (remainingDelay > 0f)
        {
            yield return new WaitForSeconds(remainingDelay);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] 2回目: {secondDelay:F2}秒経過 - テレポート位置へ移動: {teleportPosition}");
        }

        // 2回目のテレポートと歩行を実行
        yield return StartCoroutine(ExecuteTeleportAndWalk());

        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] テレポートシーケンス完了 - すべての出現が終了しました");
        }
    }

    /// <summary>
    /// テレポートと歩行を実行するコルーチン
    /// </summary>
    private IEnumerator ExecuteTeleportAndWalk()
    {
        // テレポート位置に移動（Y座標を0.978に統一）
        Vector3 teleportPos = teleportPosition;
        teleportPos.y = 0.978f;
        transform.position = teleportPos;
        if (_characterController != null)
        {
            _characterController.enabled = false;
            _characterController.transform.position = teleportPos;
            _characterController.enabled = true;
        }

        // 目標位置を設定して歩行開始（Y座標を0.978に統一）
        Vector3 targetPos = walkTargetPosition;
        targetPos.y = 0.978f;
        _currentTarget = targetPos;
        _isMoving = true;
        UpdateAnimation(true, walkSpeed);

        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] 歩行開始 - 目標位置: {walkTargetPosition}");
        }

        // 目標位置に到着するまで待機（Update()のMoveToTarget()が移動を制御）
        while (_isMoving)
        {
            // Update()のMoveToTarget()が到着判定を行い、_isMovingをfalseにする
            yield return null;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] 目標位置に到着しました: {walkTargetPosition}");
        }

        // 元の位置にテレポート（Y座標を0.978に統一）
        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] 歩行終了 - 元の位置にテレポート: {startPosition}");
        }

        Vector3 returnPos = startPosition;
        returnPos.y = 0.978f;
        transform.position = returnPos;
        if (_characterController != null)
        {
            _characterController.enabled = false;
            _characterController.transform.position = returnPos;
            _characterController.enabled = true;
        }

        // 歩行を停止
        _isMoving = false;
        UpdateAnimation(false, 0f);

        if (showDebugInfo)
        {
            Debug.Log($"[HumanMWalker] テレポートと歩行が完了しました");
        }
    }

}
