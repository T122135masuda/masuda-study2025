using UnityEngine;

public class HumanMWalker : MonoBehaviour
{
    [Header("Walking Settings")]
    [Tooltip("歩行速度（m/s）")]
    public float walkSpeed = 2.0f;

    [Tooltip("開始位置")]
    public Vector3 startPosition = new Vector3(0.48f, 0.318f, -11.301f);

    [Tooltip("目標位置")]
    public Vector3 targetPosition = new Vector3(0.48f, 0.2035494f, 13.498f);

    [Tooltip("到着判定の距離（m）")]
    public float arrivalDistance = 0.1f;

    [Tooltip("到着後の待機時間（秒）")]
    public float waitTime = 1.0f;

    [Tooltip("特定位置での待機時間（秒）")]
    public float specialWaitTime = 3.0f;

    [Tooltip("エンターキー押下後の待機時間（秒）")]
    public float startDelayTime = 3.0f;

    [Tooltip("ジャンプ回数")]
    public int jumpCount = 3;

    [Tooltip("1回のジャンプ時間（秒）")]
    public float singleJumpTime = 1.0f;

    [Tooltip("特定待機位置")]
    public Vector3 specialWaitPosition = new Vector3(0.48f, 0.2035494f, 0.76f);

    [Tooltip("自動開始")]
    public bool autoStart = false;

    [Header("Animation")]
    [Tooltip("アニメーション速度の調整")]
    public float animationSpeed = 1.0f;

    [Tooltip("歩行アニメーションコントローラー")]
    public RuntimeAnimatorController walkAnimatorController;

    [Tooltip("ジャンプアニメーションコントローラー")]
    public RuntimeAnimatorController jumpAnimatorController;

    [Tooltip("歩行アニメーションクリップ")]
    public AnimationClip walkAnimationClip;

    [Tooltip("ジャンプアニメーションクリップ")]
    public AnimationClip jumpAnimationClip;

    [Tooltip("アニメーションのブレンド時間")]
    public float animationBlendTime = 0.2f;

    [Header("Collision Avoidance")]
    [Tooltip("他のオブジェクトとの衝突を避ける")]
    public bool enableCollisionAvoidance = true;

    [Tooltip("衝突回避の検出距離")]
    public float avoidanceDistance = 1.0f;

    [Tooltip("衝突回避の力の強さ")]
    public float avoidanceForce = 2.0f;

    [Header("Jump Animation Settings")]
    [Tooltip("待機中のジャンプアニメーションを有効にする")]
    public bool enableWaitJumpAnimation = true;

    [Tooltip("ジャンプアニメーションの再生速度")]
    public float jumpAnimationSpeed = 1.0f;

    [Tooltip("ジャンプアニメーションが見つからない場合の代替手段")]
    public bool useWalkAnimationAsJump = true;

    [Header("Debug")]
    [Tooltip("デバッグ情報を表示")]
    public bool showDebugInfo = true;

    private Vector3 _currentTarget;
    private bool _isMoving = false;
    private bool _isWaiting = false;
    private float _waitTimer = 0f;
    private bool _isDelayedStart = false;
    private float _delayTimer = 0f;
    private int _currentJumpCount = 0;
    private float _jumpTimer = 0f;
    private Animator _animator;
    private CharacterController _characterController;

    // 待機中のジャンプアニメーション関連（無効化）
    // private bool _isPlayingJumpAnimation = false; // 未使用のため削除
    // private Animation _animationComponent; // Legacyアニメーションを無効化

    // 移動段階の管理
    private enum MovementPhase
    {
        WalkingToJump,      // ジャンプ位置へ歩行中
        Jumping,            // ジャンプ中
        WalkingToGoal       // ゴール位置へ歩行中
    }
    private MovementPhase _currentPhase = MovementPhase.WalkingToJump;

    // アニメーショントリガー
    private static readonly int WalkTriggerHash = Animator.StringToHash("Walk");
    private static readonly int IdleTriggerHash = Animator.StringToHash("Idle");
    private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");

    private void Start()
    {
        // コンポーネントの取得
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
        // _animationComponent = GetComponent<Animation>(); // Legacyアニメーションを無効化

        // 初期位置を確実に設定
        transform.position = startPosition;
        _currentTarget = specialWaitPosition; // 最初はジャンプ位置を目標にする
        _currentPhase = MovementPhase.WalkingToJump;

        if (showDebugInfo)
        {
            Debug.Log($"HumanMWalker: 初期位置を設定しました - {startPosition}");
            Debug.Log($"HumanMWalker: 実際の位置 - {transform.position}");
        }

        // アニメーション設定
        SetupAnimation();

        // CharacterController設定
        SetupCharacterController();

        // 初期状態はIdleアニメーション
        UpdateAnimation(false, 0f);

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

        if (jumpAnimationClip == null)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: ジャンプアニメーションクリップを検索開始...");
            }

            // Resourcesフォルダから検索
            string[] jumpClipNames = {
                "HumanM@Jump01",
                "HumanF@Jump01",
                "HumanM@Jump01_Up",
                "HumanF@Jump01_Up"
            };

            if (showDebugInfo)
            {
                Debug.Log($"HumanMWalker: Resourcesフォルダから検索中... {string.Join(", ", jumpClipNames)}");
            }

            foreach (string clipName in jumpClipNames)
            {
                jumpAnimationClip = Resources.Load<AnimationClip>(clipName);
                if (jumpAnimationClip != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: ジャンプアニメーションクリップを発見: {clipName}");
                    }
                    break;
                }
            }

            // Resourcesフォルダから見つからない場合は、シーン内のアセットから検索
            if (jumpAnimationClip == null)
            {
                if (showDebugInfo)
                {
                    Debug.Log("HumanMWalker: シーン内のアセットから検索中...");
                }
                jumpAnimationClip = FindJumpAnimationClipInScene();
            }

            // それでも見つからない場合は、AnimatorController内のアニメーションを検索
            if (jumpAnimationClip == null && _animator != null && _animator.runtimeAnimatorController != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"HumanMWalker: AnimatorController内から検索中: {_animator.runtimeAnimatorController.name}");
                }
                jumpAnimationClip = FindJumpAnimationInController(_animator.runtimeAnimatorController);
            }

            if (jumpAnimationClip == null && showDebugInfo)
            {
                Debug.Log("HumanMWalker: ジャンプアニメーションクリップが見つかりませんでした。Inspectorで手動設定してください。");
            }
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log($"HumanMWalker: ジャンプアニメーションクリップが既に設定されています: {jumpAnimationClip.name}");
            }
        }

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
                Debug.Log("移動手順:");
                Debug.Log($"1. スタート位置: {startPosition}");
                Debug.Log($"2. ジャンプ位置: {specialWaitPosition} (3秒間ジャンプ)");
                Debug.Log($"3. ゴール位置: {targetPosition}");
            }

            // エンターキーを押した場合は、3秒待機してから開始
            _isDelayedStart = true;
            _delayTimer = startDelayTime;
            _isMoving = false;
            _isWaiting = false;

            // 強制的に初期位置に設定
            transform.position = startPosition;
            if (_characterController != null)
            {
                _characterController.enabled = false;
                _characterController.transform.position = startPosition;
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
                // 待機完了、歩行開始
                _isDelayedStart = false;
                _currentPhase = MovementPhase.WalkingToJump;
                _currentTarget = specialWaitPosition;
                _isMoving = true;
                _isWaiting = false;

                // 歩行アニメーションに切り替え
                UpdateAnimation(true, walkSpeed);

                if (showDebugInfo)
                {
                    Debug.Log("=== 待機完了、歩行開始 ===");
                    Debug.Log($"HumanMWalker: 現在の段階: {_currentPhase}");
                    Debug.Log($"HumanMWalker: 次の目標: {_currentTarget}");
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
            // ジャンプ中はジャンプアニメーションを再生
            if (enableWaitJumpAnimation)
            {
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"HumanMWalker: ジャンプ中 - ジャンプアニメーション再生中");
                }
                UpdateAnimation(false, 0f, true); // ジャンプアニメーションを実行
            }
            else
            {
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log("HumanMWalker: ジャンプ中 - ジャンプアニメーションが無効です");
                }
                UpdateAnimation(false, 0f); // 通常の待機アニメーション
            }
        }
        else
        {
            // 移動も待機もしていない場合の処理
            UpdateAnimation(false, 0f);
            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"HumanMWalker: 待機状態 - 段階: {_currentPhase}, 移動中: {_isMoving}, 待機中: {_isWaiting}");
            }
        }
    }

    private void MoveToTarget()
    {
        // 目標までの距離を計算
        float distanceToTarget = Vector3.Distance(transform.position, _currentTarget);

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
        }
        else
        {
            // CharacterControllerがない場合は直接位置変更
            transform.position += movement;
        }

        // 回転（移動方向を向く）
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 5f * Time.deltaTime);
        }

        // デバッグ情報（一定間隔で）
        if (showDebugInfo && Time.frameCount % 120 == 0) // 2秒に1回
        {
            Debug.Log($"HumanMWalker: 移動中 - 段階: {_currentPhase}, 現在位置: {transform.position}, 目標: {_currentTarget}, 距離: {distanceToTarget:F2}");
            Debug.Log($"HumanMWalker: 移動方向: {direction}, 移動量: {movement}");
        }
    }


    private void ArrivedAtTarget()
    {
        _isMoving = false;

        switch (_currentPhase)
        {
            case MovementPhase.WalkingToJump:
                // ジャンプ位置に到着
                _currentPhase = MovementPhase.Jumping;
                _isWaiting = true;
                _currentJumpCount = 1; // 1回目から開始
                _jumpTimer = singleJumpTime;
                if (showDebugInfo)
                {
                    Debug.Log("=== ジャンプ位置に到着 ===");
                    Debug.Log($"HumanMWalker: ジャンプ位置に到着しました: {specialWaitPosition}");
                    Debug.Log($"HumanMWalker: {jumpCount}回のジャンプを開始します（1回あたり{singleJumpTime}秒）");
                    Debug.Log($"HumanMWalker: 1回目のジャンプ開始");
                    Debug.Log($"HumanMWalker: 現在の段階: {_currentPhase}");
                }
                break;

            case MovementPhase.WalkingToGoal:
                // ゴール位置に到着
                if (showDebugInfo)
                {
                    Debug.Log("=== ゴール位置に到着 ===");
                    Debug.Log($"HumanMWalker: ゴール位置に到着しました: {targetPosition}");
                    Debug.Log("HumanMWalker: 移動完了。");
                }
                break;
        }
    }

    private void HandleWaiting()
    {
        if (_currentPhase == MovementPhase.Jumping)
        {
            // ジャンプ中の処理
            _jumpTimer -= Time.deltaTime;
            if (_jumpTimer <= 0f)
            {
                if (_currentJumpCount < jumpCount)
                {
                    // 次のジャンプへ
                    _currentJumpCount++;
                    _jumpTimer = singleJumpTime;
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: {_currentJumpCount - 1}回目のジャンプ完了。{_currentJumpCount}回目を開始します。");
                    }
                }
                else
                {
                    // 全ジャンプ完了
                    _isWaiting = false;
                    _currentPhase = MovementPhase.WalkingToGoal;
                    _currentTarget = targetPosition;
                    _isMoving = true;
                    if (showDebugInfo)
                    {
                        Debug.Log("=== 全ジャンプ完了、ゴールへ移動開始 ===");
                        Debug.Log($"HumanMWalker: {jumpCount}回のジャンプ完了。ゴール位置へ移動開始。");
                        Debug.Log($"HumanMWalker: ゴール位置: {targetPosition}");
                        Debug.Log($"HumanMWalker: 段階: {_currentPhase}, 目標: {_currentTarget}, 移動中: {_isMoving}");
                    }
                }
            }
            else
            {
                // ジャンプ中のデバッグ情報
                if (showDebugInfo && Time.frameCount % 60 == 0) // 1秒に1回
                {
                    Debug.Log($"HumanMWalker: ジャンプ中 - {_currentJumpCount}回目, 残り時間: {_jumpTimer:F1}秒");
                }
            }
        }
        else
        {
            // その他の待機処理（通常の待機タイマー）
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
    }

    public void StartWalking()
    {
        // 常に最初から開始
        _currentPhase = MovementPhase.WalkingToJump;

        // 強制的に初期位置に設定
        transform.position = startPosition;
        if (_characterController != null)
        {
            _characterController.enabled = false;
            _characterController.transform.position = startPosition;
            _characterController.enabled = true;
        }

        // ジャンプ位置を目標に設定
        _currentTarget = specialWaitPosition;

        _isMoving = true;
        _isWaiting = false;

        // 歩行アニメーションに切り替え
        UpdateAnimation(true, walkSpeed);

        if (showDebugInfo)
        {
            Debug.Log("HumanMWalker: 歩行開始。最初からやり直します。");
            Debug.Log($"HumanMWalker: 設定位置: {startPosition}");
            Debug.Log($"HumanMWalker: 実際の位置: {transform.position}");
            Debug.Log($"HumanMWalker: 歩行開始。段階: {_currentPhase}, 目標: {_currentTarget}");
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
        if (!_isMoving && !_isWaiting)
        {
            transform.position = startPosition;
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

    private void UpdateAnimation(bool isWalking, float speed, bool isJumping = false)
    {
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"HumanMWalker: UpdateAnimation呼び出し - 歩行:{isWalking}, 速度:{speed:F2}, ジャンプ:{isJumping}");
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
        if (isJumping && enableWaitJumpAnimation)
        {
            // ジャンプアニメーション
            if (jumpAnimationClip != null)
            {
                _animator.Play(jumpAnimationClip.name);
                _animator.speed = jumpAnimationSpeed;

                if (showDebugInfo)
                {
                    Debug.Log($"HumanMWalker: ジャンプアニメーション再生: {jumpAnimationClip.name}");
                }
            }
            else
            {
                // ジャンプアニメーションが見つからない場合は歩行アニメーションを代用
                if (walkAnimationClip != null)
                {
                    _animator.Play(walkAnimationClip.name);
                    _animator.speed = jumpAnimationSpeed;
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: ジャンプアニメーションが見つからないため、歩行アニメーションを代用: {walkAnimationClip.name}");
                    }
                }
                else
                {
                    if (showDebugInfo)
                    {
                        Debug.Log("HumanMWalker: アニメーションクリップが設定されていません");
                    }
                }
            }
        }
        else if (isWalking && speed > 0f)
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
            Debug.Log($"HumanMWalker: アニメーション状態 - 歩行中:{isWalking}, ジャンプ中:{isJumping}, 速度:{speed:F2}, Animator速度:{_animator.speed:F2}");
            Debug.Log($"HumanMWalker: Animator Controller: {(_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "None")}");
            Debug.Log($"HumanMWalker: 歩行アニメーションクリップ: {(walkAnimationClip != null ? walkAnimationClip.name : "None")}");
            Debug.Log($"HumanMWalker: ジャンプアニメーションクリップ: {(jumpAnimationClip != null ? jumpAnimationClip.name : "None")}");
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

    private AnimationClip FindJumpAnimationClipInScene()
    {
        // シーン内のすべてのアニメーションクリップを検索
        AnimationClip[] allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();

        if (showDebugInfo)
        {
            Debug.Log($"HumanMWalker: シーン内で{allClips.Length}個のアニメーションクリップを検索中...");
        }

        string[] jumpKeywords = { "Jump", "jump", "JUMP" };

        foreach (AnimationClip clip in allClips)
        {
            if (clip == null) continue;

            string clipName = clip.name;

            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"HumanMWalker: 検索中: {clipName}");
            }

            // ジャンプ関連のキーワードを含むクリップを検索
            foreach (string keyword in jumpKeywords)
            {
                if (clipName.Contains(keyword))
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: ジャンプアニメーションクリップを発見: {clipName}");
                    }
                    return clip;
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log("HumanMWalker: シーン内でジャンプアニメーションクリップが見つかりませんでした");
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

    private AnimationClip FindJumpAnimationInController(RuntimeAnimatorController controller)
    {
        if (controller == null) return null;

        // AnimatorController内のすべてのアニメーションクリップを検索
        foreach (var layer in controller.animationClips)
        {
            if (layer == null) continue;

            string clipName = layer.name;
            string[] jumpKeywords = { "Jump", "jump", "JUMP" };

            foreach (string keyword in jumpKeywords)
            {
                if (clipName.Contains(keyword))
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: AnimatorController内でジャンプアニメーションクリップを発見: {clipName}");
                    }
                    return layer;
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log("HumanMWalker: AnimatorController内でジャンプアニメーションクリップが見つかりませんでした");
        }

        return null;
    }

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
        GUILayout.Label($"待機中ジャンプアニメーション: {enableWaitJumpAnimation}");
        GUILayout.Label($"現在の段階: {_currentPhase}");
        GUILayout.Label($"現在位置: {transform.position}");
        GUILayout.Label($"設定開始位置: {startPosition}");
        GUILayout.Label($"現在の目標: {_currentTarget}");

        // 位置の差を表示
        Vector3 positionDiff = transform.position - startPosition;
        GUILayout.Label($"位置差: {positionDiff}");
        GUILayout.Label($"歩行速度: {walkSpeed:F2} m/s");
        GUILayout.Label($"特定待機位置: {specialWaitPosition}");
        GUILayout.Label($"ジャンプ回数: {jumpCount}回");
        GUILayout.Label($"1回のジャンプ時間: {singleJumpTime}秒");
        if (_isWaiting)
        {
            if (_currentPhase == MovementPhase.Jumping)
            {
                GUILayout.Label($"ジャンプ中: {_currentJumpCount}/{jumpCount}回目");
                GUILayout.Label($"ジャンプ残り時間: {_jumpTimer:F1}秒");
            }
            else
            {
                GUILayout.Label($"待機残り時間: {_waitTimer:F1}秒");
            }
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
        GUILayout.Label("エンターキー: 3秒待機後に歩行開始");
        GUILayout.Label("移動手順:");
        GUILayout.Label("1. エンターキー押下 → 3秒待機");
        GUILayout.Label("2. スタート位置 → ジャンプ位置へ歩行");
        GUILayout.Label($"3. ジャンプ位置で{jumpCount}回ジャンプ");
        GUILayout.Label("4. ジャンプ位置 → ゴール位置へ歩行");

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
        GUILayout.Label("ジャンプアニメーション:", GUI.skin.box);
        GUILayout.Label($"有効: {enableWaitJumpAnimation}");
        GUILayout.Label($"クリップ: {(jumpAnimationClip != null ? jumpAnimationClip.name : "None")}");
        GUILayout.Label($"速度: {jumpAnimationSpeed:F2}");
        GUILayout.Label($"歩行アニメーション代用: {useWalkAnimationAsJump}");

        GUILayout.Label("Animation: 無効化されています（Legacyアニメーションエラー回避のため）", GUI.skin.box);

        GUILayout.EndArea();
    }
}
