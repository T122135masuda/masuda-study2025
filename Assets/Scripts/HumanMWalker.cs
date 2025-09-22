using UnityEngine;

public class HumanMWalker : MonoBehaviour
{
    [Header("Walking Settings")]
    [Tooltip("歩行速度（m/s）")]
    public float walkSpeed = 2.0f;
    
    [Tooltip("開始位置")]
    public Vector3 startPosition = new Vector3(0.48f, 0.2035494f, -12.12f);
    
    [Tooltip("目標位置")]
    public Vector3 targetPosition = new Vector3(0.48f, 0.2035494f, 13.498f);
    
    [Tooltip("到着判定の距離（m）")]
    public float arrivalDistance = 0.1f;
    
    [Tooltip("到着後の待機時間（秒）")]
    public float waitTime = 1.0f;
    
    [Tooltip("特定位置での待機時間（秒）")]
    public float specialWaitTime = 3.0f;
    
    [Tooltip("特定待機位置")]
    public Vector3 specialWaitPosition = new Vector3(0.48f, 0.2035494f, 0.76f);
    
    [Tooltip("自動開始")]
    public bool autoStart = false;
    
    [Header("Animation")]
    [Tooltip("アニメーション速度の調整")]
    public float animationSpeed = 1.0f;
    
    [Tooltip("歩行アニメーションコントローラー")]
    public RuntimeAnimatorController walkAnimatorController;
    
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
    private Animator _animator;
    private CharacterController _characterController;
    
    // 待機中のジャンプアニメーション関連
    private bool _isPlayingJumpAnimation = false;
    private Animation _animationComponent;
    
    // 移動段階の管理
    private enum MovementPhase
    {
        ToSpecialPosition,  // 特定位置へ移動中
        AtSpecialPosition,  // 特定位置で待機中
        ToGoalPosition,     // ゴール位置へ移動中
        AtGoalPosition      // ゴール位置に到達（完了）
    }
    private MovementPhase _currentPhase = MovementPhase.ToSpecialPosition;
    
    // アニメーションパラメータ
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsIdleHash = Animator.StringToHash("IsIdle");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    
    private void Start()
    {
        // コンポーネントの取得
        _animator = GetComponent<Animator>();
        _characterController = GetComponent<CharacterController>();
        _animationComponent = GetComponent<Animation>();
        
        // 初期位置を確実に設定
        transform.position = startPosition;
        _currentTarget = specialWaitPosition; // 最初は特定位置を目標にする
        _currentPhase = MovementPhase.ToSpecialPosition;
        
        if (showDebugInfo)
        {
            Debug.Log($"HumanMWalker: 初期位置を設定しました - {startPosition}");
            Debug.Log($"HumanMWalker: 実際の位置 - {transform.position}");
        }
        
        // アニメーション設定
        SetupAnimation();
        
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
            Debug.LogError("HumanMWalker: Animatorコンポーネントが見つかりません！");
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
            walkAnimationClip = Resources.Load<AnimationClip>("HumanM@Walk01_Forward");
            if (walkAnimationClip == null)
            {
                walkAnimationClip = Resources.Load<AnimationClip>("HumanM@Walk01_ForwardRight");
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
                Debug.LogWarning("HumanMWalker: ジャンプアニメーションクリップが見つかりませんでした");
                Debug.LogWarning("HumanMWalker: Inspectorで手動でジャンプアニメーションクリップを設定してください");
                Debug.LogWarning("HumanMWalker: または、useWalkAnimationAsJumpをtrueにして歩行アニメーションを代用してください");
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
            Debug.LogWarning("HumanMWalker: 歩行アニメーションコントローラーが見つかりませんでした。");
            if (showDebugInfo)
            {
                Debug.LogWarning("HumanMWalker: 手動でアニメーションコントローラーを設定してください。");
            }
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
            
            // エンターキーを押した場合は、常に最初から開始
            _currentPhase = MovementPhase.ToSpecialPosition;
            
            // 強制的に初期位置に設定
            transform.position = startPosition;
            if (_characterController != null)
            {
                _characterController.enabled = false;
                _characterController.transform.position = startPosition;
                _characterController.enabled = true;
            }
            
            _currentTarget = specialWaitPosition;
            _isMoving = true;
            _isWaiting = false;
            
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: エンターキーで歩行開始。最初からやり直します。");
                Debug.Log($"HumanMWalker: 現在の段階: {_currentPhase}");
                Debug.Log($"HumanMWalker: 設定位置: {startPosition}");
                Debug.Log($"HumanMWalker: 実際の位置: {transform.position}");
                Debug.Log($"HumanMWalker: 次の目標: {_currentTarget}");
                Debug.Log("=== 移動開始 ===");
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
            // 待機中はジャンプアニメーションを再生
            if (enableWaitJumpAnimation)
            {
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"HumanMWalker: 待機中 - ジャンプアニメーション再生を試行中...");
                    Debug.Log($"HumanMWalker: jumpAnimationClip = {(jumpAnimationClip != null ? jumpAnimationClip.name : "null")}");
                    Debug.Log($"HumanMWalker: useWalkAnimationAsJump = {useWalkAnimationAsJump}");
                    Debug.Log($"HumanMWalker: walkAnimationClip = {(walkAnimationClip != null ? walkAnimationClip.name : "null")}");
                }
                UpdateAnimation(false, 0f, true);
            }
            else
            {
                if (showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.Log("HumanMWalker: 待機中 - ジャンプアニメーションが無効です");
                }
                UpdateAnimation(false, 0f);
            }
        }
        else
        {
            // 移動も待機もしていない場合の処理
            if (_currentPhase == MovementPhase.AtGoalPosition)
            {
                // ゴール位置に到達済み
                UpdateAnimation(false, 0f);
            }
            else
            {
                // その他の場合は待機状態にする
                UpdateAnimation(false, 0f);
                if (showDebugInfo)
                {
                    Debug.LogWarning($"HumanMWalker: 予期しない状態 - 段階: {_currentPhase}, 移動中: {_isMoving}, 待機中: {_isWaiting}");
                }
            }
        }
    }
    
    private void MoveToTarget()
    {
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
        
        // 移動
        Vector3 movement = direction * walkSpeed * Time.deltaTime;
        if (_characterController != null)
        {
            _characterController.Move(movement);
        }
        else
        {
            transform.position += movement;
        }
        
        // 回転（移動方向を向く）
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 5f * Time.deltaTime);
        }
        
        // 到着判定
        float distanceToTarget = Vector3.Distance(transform.position, _currentTarget);
        if (distanceToTarget <= arrivalDistance)
        {
            if (showDebugInfo)
            {
                Debug.Log($"HumanMWalker: 目標に到着。現在位置: {transform.position}, 目標: {_currentTarget}, 距離: {distanceToTarget:F2}");
            }
            ArrivedAtTarget();
        }
        
        // デバッグ情報（一定間隔で）
        if (showDebugInfo && Time.frameCount % 120 == 0) // 2秒に1回
        {
            Debug.Log($"HumanMWalker: 移動中 - 段階: {_currentPhase}, 現在位置: {transform.position}, 目標: {_currentTarget}, 距離: {distanceToTarget:F2}");
        }
    }
    
    private void ArrivedAtTarget()
    {
        _isMoving = false;
        
        switch (_currentPhase)
        {
            case MovementPhase.ToSpecialPosition:
                // 特定位置に到着
                _currentPhase = MovementPhase.AtSpecialPosition;
                _isWaiting = true;
                _waitTimer = specialWaitTime;
                if (showDebugInfo)
                {
                    Debug.Log("=== ジャンプ位置に到着 ===");
                    Debug.Log($"HumanMWalker: 特定位置に到着しました: {specialWaitPosition}");
                    Debug.Log($"HumanMWalker: {specialWaitTime}秒間ジャンプアニメーションを再生します");
                    Debug.Log($"HumanMWalker: 現在の段階: {_currentPhase}");
                }
                break;
                
            case MovementPhase.ToGoalPosition:
                // ゴール位置に到着
                _currentPhase = MovementPhase.AtGoalPosition;
                _isWaiting = false; // ゴールでは待機しない
                _waitTimer = 0f;
                if (showDebugInfo)
                {
                    Debug.Log("=== ゴール位置に到着 ===");
                    Debug.Log($"HumanMWalker: ゴール位置に到着しました: {targetPosition}");
                    Debug.Log("HumanMWalker: 移動完了。");
                    Debug.Log($"HumanMWalker: 現在の段階: {_currentPhase}");
                }
                break;
        }
    }
    
    private void HandleWaiting()
    {
        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0f)
        {
            _isWaiting = false;
            
            switch (_currentPhase)
            {
                case MovementPhase.AtSpecialPosition:
                    // 特定位置での待機完了、ゴール位置へ移動開始
                    _currentPhase = MovementPhase.ToGoalPosition;
                    _currentTarget = targetPosition;
                    _isMoving = true; // 直接移動状態にする
                    if (showDebugInfo)
                    {
                        Debug.Log("=== ジャンプ完了、ゴールへ移動開始 ===");
                        Debug.Log($"HumanMWalker: 特定位置での待機完了。ゴール位置へ移動開始。");
                        Debug.Log($"HumanMWalker: ゴール位置: {targetPosition}");
                        Debug.Log($"HumanMWalker: 段階: {_currentPhase}, 目標: {_currentTarget}, 移動中: {_isMoving}");
                    }
                    break;
                    
                case MovementPhase.AtGoalPosition:
                    // ゴール位置での処理（現在は何もしない）
                    if (showDebugInfo)
                    {
                        Debug.Log("HumanMWalker: ゴール位置に到達済み。移動完了。");
                    }
                    break;
            }
        }
        else
        {
            // 待機中のデバッグ情報
            if (showDebugInfo && Time.frameCount % 60 == 0) // 1秒に1回
            {
                Debug.Log($"HumanMWalker: 待機中 - 段階: {_currentPhase}, 残り時間: {_waitTimer:F1}秒");
            }
        }
    }
    
    public void StartWalking()
    {
        // エンターキーで開始する場合は、最初からやり直す
        if (_currentPhase == MovementPhase.AtGoalPosition)
        {
            // ゴール位置に到達済みの場合は、最初からやり直す
            _currentPhase = MovementPhase.ToSpecialPosition;
            
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
                Debug.Log("HumanMWalker: ゴール到達済み。最初からやり直します。");
                Debug.Log($"HumanMWalker: 設定位置: {startPosition}");
                Debug.Log($"HumanMWalker: 実際の位置: {transform.position}");
            }
        }
        
        // 現在の段階に応じて目標を設定
        switch (_currentPhase)
        {
            case MovementPhase.ToSpecialPosition:
                _currentTarget = specialWaitPosition;
                break;
            case MovementPhase.ToGoalPosition:
                _currentTarget = targetPosition;
                break;
        }
        
        _isMoving = true;
        _isWaiting = false;
        Debug.Log($"HumanMWalker: 歩行開始。段階: {_currentPhase}, 目標: {_currentTarget}");
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
        
        // ジャンプアニメーションの場合はAnimationコンポーネントを使用
        if (isJumping && enableWaitJumpAnimation)
        {
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: ジャンプアニメーション処理開始");
            }
            
            if (jumpAnimationClip == null)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("HumanMWalker: ジャンプアニメーションクリップが設定されていません");
                }
                
                // 代替手段が有効な場合は歩行アニメーションを使用
                if (useWalkAnimationAsJump && walkAnimationClip != null)
                {
                    jumpAnimationClip = walkAnimationClip;
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: 歩行アニメーションをジャンプ用に使用: {walkAnimationClip.name}");
                    }
                }
                else
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning("HumanMWalker: ジャンプアニメーションクリップが見つからず、代替手段も無効です");
                        Debug.LogWarning("HumanMWalker: InspectorでjumpAnimationClipを手動設定するか、useWalkAnimationAsJumpをtrueにしてください");
                    }
                    return;
                }
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.Log($"HumanMWalker: ジャンプアニメーションクリップが設定済み: {jumpAnimationClip.name}");
                }
            }
            
            if (_animationComponent == null)
            {
                _animationComponent = gameObject.AddComponent<Animation>();
                if (showDebugInfo)
                {
                    Debug.Log("HumanMWalker: Animationコンポーネントを追加しました");
                }
            }
            
            if (!_animationComponent.isPlaying || _animationComponent.clip != jumpAnimationClip)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"HumanMWalker: アニメーション再生開始 - 現在再生中: {_animationComponent.isPlaying}");
                    Debug.Log($"HumanMWalker: 現在のクリップ: {(_animationComponent.clip != null ? _animationComponent.clip.name : "null")}");
                    Debug.Log($"HumanMWalker: 設定するクリップ: {jumpAnimationClip.name}");
                }
                
                // Animatorを一時的に無効化（競合を避けるため）
                if (_animator != null && _animator.enabled)
                {
                    _animator.enabled = false;
                    if (showDebugInfo)
                    {
                        Debug.Log("HumanMWalker: Animatorを一時的に無効化しました");
                    }
                }
                
                // アニメーションクリップを設定して再生
                if (jumpAnimationClip != null)
                {
                    _animationComponent.clip = jumpAnimationClip;
                    _animationComponent.Play();
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"HumanMWalker: アニメーションクリップを設定: {jumpAnimationClip.name}");
                        Debug.Log($"HumanMWalker: アニメーション長さ: {jumpAnimationClip.length}秒");
                    }
                }
                else
                {
                    if (showDebugInfo)
                    {
                        Debug.LogError("HumanMWalker: jumpAnimationClipがnullです");
                    }
                    return;
                }
                
                // アニメーション速度を安全に設定
                if (jumpAnimationClip != null && !string.IsNullOrEmpty(jumpAnimationClip.name))
                {
                    try
                    {
                        _animationComponent[jumpAnimationClip.name].speed = jumpAnimationSpeed;
                    }
                    catch (System.Exception e)
                    {
                        if (showDebugInfo)
                        {
                            Debug.LogWarning($"HumanMWalker: アニメーション速度設定に失敗: {e.Message}");
                        }
                    }
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"HumanMWalker: ジャンプアニメーションを再生開始: {jumpAnimationClip.name}");
                    Debug.Log($"HumanMWalker: Animation再生状態: {_animationComponent.isPlaying}");
                    Debug.Log($"HumanMWalker: Animation速度: {jumpAnimationSpeed}");
                    Debug.Log($"HumanMWalker: Animation有効: {_animationComponent.enabled}");
                    Debug.Log($"HumanMWalker: 現在のクリップ: {(_animationComponent.clip != null ? _animationComponent.clip.name : "null")}");
                }
            }
            else if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"HumanMWalker: ジャンプアニメーション再生中: {jumpAnimationClip.name}");
                Debug.Log($"HumanMWalker: 再生状態確認 - isPlaying: {_animationComponent.isPlaying}, enabled: {_animationComponent.enabled}");
                
                // アニメーションが停止している場合は再開を試行
                if (!_animationComponent.isPlaying && _animationComponent.clip != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning("HumanMWalker: アニメーションが停止しています。再開を試行します。");
                    }
                    _animationComponent.Play();
                }
            }
            return;
        }
        
        // 歩行アニメーションの場合はAnimatorコンポーネントを使用
        if (_animator == null) 
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("HumanMWalker: Animatorがnullです。アニメーションを更新できません。");
            }
            return;
        }
        
        // Animatorが無効の場合は有効化（ジャンプから歩行に戻る場合）
        if (!_animator.enabled)
        {
            _animator.enabled = true;
            if (showDebugInfo)
            {
                Debug.Log("HumanMWalker: Animatorを再有効化しました（ジャンプから歩行に戻る）");
            }
        }
        
        // アニメーションパラメータを更新
        _animator.SetFloat(SpeedHash, speed);
        _animator.SetBool(IsWalkingHash, isWalking);
        _animator.SetBool(IsIdleHash, !isWalking);
        _animator.SetBool(IsJumpingHash, false);
        
        // アニメーション速度を歩行速度に合わせて調整
        if (isWalking && speed > 0f)
        {
            _animator.speed = animationSpeed * (speed / 2.0f); // 基準速度2.0fで正規化
        }
        else
        {
            _animator.speed = animationSpeed;
        }
        
        // デバッグ情報を表示（一定間隔で）
        if (showDebugInfo && Time.frameCount % 60 == 0) // 1秒に1回
        {
            Debug.Log($"HumanMWalker: アニメーション状態 - 歩行中:{isWalking}, ジャンプ中:{isJumping}, 速度:{speed:F2}, Animator速度:{_animator.speed:F2}");
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
            Debug.LogWarning("HumanMWalker: シーン内でジャンプアニメーションクリップが見つかりませんでした");
            Debug.LogWarning("HumanMWalker: 利用可能なアニメーションクリップ:");
            foreach (AnimationClip clip in allClips)
            {
                if (clip != null)
                {
                    Debug.LogWarning($"  - {clip.name}");
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
            Debug.LogWarning("HumanMWalker: AnimatorController内でジャンプアニメーションクリップが見つかりませんでした");
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
        GUILayout.Label($"特定待機時間: {specialWaitTime}秒");
        if (_isWaiting)
        {
            GUILayout.Label($"待機残り時間: {_waitTimer:F1}秒");
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
        GUILayout.Label("エンターキー: 最初から歩行開始");
        GUILayout.Label("移動手順:");
        GUILayout.Label("1. スタート位置 (0.48, 0.2035494, -12.12)");
        GUILayout.Label("2. ジャンプ位置 (0.48, 0.2035494, 0.76)");
        GUILayout.Label("   → 3秒間ジャンプアニメーション再生");
        GUILayout.Label("3. ゴール位置 (0.48, 0.2035494, 13.498)");
        
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
            
            if (_animationComponent != null)
            {
                GUILayout.Label($"Animation再生中: {_animationComponent.isPlaying}");
                GUILayout.Label($"Animationクリップ: {(_animationComponent.clip != null ? _animationComponent.clip.name : "None")}");
            }
            else
            {
                GUILayout.Label("Animation: 見つかりません", GUI.skin.box);
            }
        
        GUILayout.EndArea();
    }
}
