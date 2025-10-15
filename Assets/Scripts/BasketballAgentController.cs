using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BasketballAgentController : MonoBehaviour
{
    [Header("Move Params")]
    [Tooltip("最大移動速度（m/s）")]
    public float maxSpeed = 7.0f; // より高速に
    [Tooltip("加速度（m/s²）")]
    public float acceleration = 20f; // 立ち上がり強化
    [Tooltip("回転速度（度/秒）")]
    public float turnSpeedDegPerSec = 1080f; // 旋回性アップ

    [Header("Random Wander")]
    [Tooltip("ワンダーのランダム性")]
    public float wanderJitter = 4.0f; // ランダム性強化
    [Tooltip("ワンダー半径")]
    public float wanderRadius = 5.5f; // 軌道を広げる
    [Tooltip("ワンダー距離")]
    public float wanderDistance = 5.0f; // 前方距離拡大

    [Header("Separation / Avoidance")]
    [Tooltip("分離半径")]
    public float separationRadius = 0.8f; // 密集しにくく
    [Tooltip("回避半径")]
    public float avoidanceRadius = 1.4f; // 邪魔を回避
    [Tooltip("分離の強さ")]
    public float separationStrength = 6.0f; // 押し出し強化
    [Tooltip("回避の強さ")]
    public float avoidanceStrength = 10f; // 回避強化

    [Header("Boundary")]
    [Tooltip("境界からの余裕距離")]
    public float boundaryPadding = 0.3f; // 端に寄りすぎない範囲で緩和
    [Tooltip("境界押し戻しの強さ")]
    public float boundaryPushStrength = 6f;
    [Tooltip("境界からの距離に応じた力の倍率")]
    public float boundaryForceMultiplier = 2.5f; // 押し戻し強め
    [Tooltip("最大境界押し戻し力")]
    public float maxBoundaryForce = 15f; // 最大境界押し戻し力
    [Tooltip("Z軸方向の追加パディング")]
    public float zBoundaryExtraPadding = 0.8f; // Z軸方向の追加パディング
    [Tooltip("予測的境界チェック距離")]
    public float predictiveBoundaryDistance = 1.5f; // 早めに境界対応

    [Header("Wall Avoidance")]
    [Tooltip("壁検出距離")]
    public float wallDetectDistance = 1.0f; // 壁からこの距離以内で回避
    [Tooltip("壁回避の強さ")]
    public float wallAvoidStrength = 14f; // 壁際での切替強化

    [Header("Human-like Movement")]
    [Tooltip("停止する確率（0-1）")]
    [Range(0f, 0.1f)]
    public float idleChance = 0.01f; // 止まりにくく
    [Tooltip("方向転換する確率（0-1）")]
    [Range(0f, 0.2f)]
    public float directionChangeChance = 0.25f; // 向き替え頻度アップ
    [Tooltip("ダッシュする確率（0-1）")]
    [Range(0f, 0.3f)]
    public float sprintChance = 0.32f; // ダッシュ頻度アップ
    [Tooltip("ダッシュ時の速度倍率")]
    [Range(1f, 3f)]
    public float sprintMultiplier = 2.2f; // ダッシュ速度アップ

    [Header("Team Play")]
    [Tooltip("チームメイトとの結束距離")]
    public float teamCohesionRadius = 8.0f; // チームメイトとの結束距離（大幅拡大）
    [Tooltip("チーム結束の強さ")]
    public float teamCohesionStrength = 0.2f; // チーム結束の強さ（大幅弱化）
    [Tooltip("フォーメーション維持の強さ")]
    public float teamFormationStrength = 0.1f; // フォーメーション維持の強さ（大幅弱化）
    [Tooltip("相手チーム回避距離")]
    public float opponentAvoidanceRadius = 1.2f; // 相手チーム回避距離（大幅縮小）
    [Tooltip("相手チーム回避の強さ")]
    public float opponentAvoidanceStrength = 0.5f; // 相手チーム回避の強さ（大幅弱化）
    [Tooltip("チーム混在を促進する力")]
    public float teamMixingStrength = 2.8f; // 混在促進強化
    [Tooltip("チーム関連の力を無効化する")]
    public bool disableTeamForces = true; // チーム関連の力を無効化

    [Header("Pass Cut Behavior")]
    [Tooltip("パスカット機能を有効にする")]
    public bool enablePassCut = true; // パスカット機能を有効にする
    [Tooltip("パスカットの検出距離")]
    public float passCutDetectionRadius = 6.0f; // パスカットの検出距離
    [Tooltip("パスカットの強さ")]
    public float passCutStrength = 3.0f; // パスカットの強さ
    [Tooltip("パスカットの確率（0-1）")]
    [Range(0f, 1f)]
    public float passCutChance = 0.3f; // パスカットの確率

    [Header("Research - Fixed Random")]
    [Tooltip("研究用固定シード")]
    public int seed = 42; // 研究用固定シード
    [Tooltip("固定ランダムを使用するか")]
    public bool useFixedRandom = true; // 固定ランダムを使用するか

    [Header("Speed Presets")]
    [Tooltip("速度プリセット")]
    public SpeedPreset speedPreset = SpeedPreset.Fast;

    [Header("Pause Control")]
    [Tooltip("エンターキーで一時停止/再開")]
    public bool enablePauseControl = true;
    [Tooltip("初期状態で停止するか")]
    public bool startPaused = true;

    public enum SpeedPreset
    {
        Slow = 0,
        Normal = 1,
        Fast = 2,
        VeryFast = 3
    }

    private CharacterController _cc;
    private Vector3 _velocity;
    private Vector3 _wanderTarget;
    private bool _isIdle = false;
    private bool _isSprinting = false;
    private float _idleTimer = 0f;
    private float _directionChangeTimer = 0f;
    private string _teamType; // "white" or "black"
    private Vector3 _teamCenter; // チームの中心位置

    // 固定ランダム用の変数
    private float _wanderOffsetX = 0f;
    private float _wanderOffsetZ = 0f;
    private float _idleThreshold = 0f;
    private float _directionChangeThreshold = 0f;
    private float _sprintThreshold = 0f;
    private float _sprintDuration = 0f;
    private int _agentId = 0;

    [Header("Roaming")]
    [Tooltip("コート内で周期的に遠めのランダム目標へ移動する")]
    public bool enableRoam = true;
    [Tooltip("ローミングの目標切り替え間隔（秒）の範囲")]
    public Vector2 roamIntervalRange = new Vector2(2.0f, 4.0f);
    [Tooltip("ローミングのシーク強度（合力への寄与）")]
    public float roamSeekStrength = 1.2f;

    private Vector3 _roamTarget;
    private float _roamTimer = 0f;

    // ブースト機能は削除済み

    [Header("Height Variation")]
    [Tooltip("高さ変化を有効にする")]
    public bool enableHeightVariation = true;
    [Tooltip("高さ変化の速度（サイン波の角速度係数）")]
    public float heightChangeSpeed = 0.5f;
    [Tooltip("高さ変化速度の最小値／最大値")]
    public Vector2 heightChangeSpeedRange = new Vector2(0.1f, 3.0f);
    [Tooltip("高さ変化が再開されたときのスムーズ復帰時間（秒）")]
    public float heightResumeBlendDuration = 0.3f;

    // 初期値の保存
    private Vector3 _initialScale;
    private float _initialCCHeight;
    private float _initialCCCenterY;

    // 一時停止管理
    private bool _isPaused = false;
    private static bool _globalPause = false; // 全エージェント共通の一時停止状態
    // 完全凍結（外力無視）終了時刻
    private float _freezeUntil = 0f;

    private void OnEnable()
    {
        _cc = GetComponent<CharacterController>();
        CourtManager.Instance?.RegisterAgent(this);

        // エージェントIDを決定（名前から）
        DetermineAgentId();

        // 固定ランダム初期化
        if (useFixedRandom)
        {
            InitializeFixedRandom();
        }

        // 速度プリセットを適用
        ApplySpeedPreset();

        // 初期ワンダーターゲットを前方に配置
        _wanderTarget = transform.position + transform.forward * wanderDistance;

        // ローミング初期化
        ResetRoamTimer();
        PickNewRoamTarget();

        // ブースト機能は削除

        // チーム判定
        DetermineTeam();

        // 高さ変化用の初期値記録（Transform と CharacterController）
        _initialScale = transform.localScale;
        if (_cc != null)
        {
            _initialCCHeight = _cc.height;
            _initialCCCenterY = _cc.center.y;
        }

        // 高さ変化の状態を初期化
        _heightWasEnabled = enableHeightVariation;

        // 一時停止状態の初期化
        _isPaused = startPaused;
        if (_isPaused)
        {
            _velocity = Vector3.zero;
            _cc.enabled = false;
            Debug.Log($"{gameObject.name} は一時停止状態に設定されました。");
        }
    }

    private void ApplySpeedPreset()
    {
        switch (speedPreset)
        {
            case SpeedPreset.Slow:
                maxSpeed = 2.5f;
                acceleration = 6f;
                turnSpeedDegPerSec = 360f;
                sprintMultiplier = 1.3f;
                break;
            case SpeedPreset.Normal:
                maxSpeed = 5.0f;
                acceleration = 12f;
                turnSpeedDegPerSec = 720f;
                sprintMultiplier = 1.8f;
                break;
            case SpeedPreset.Fast:
                maxSpeed = 7.5f;
                acceleration = 18f;
                turnSpeedDegPerSec = 900f;
                sprintMultiplier = 2.2f;
                break;
            case SpeedPreset.VeryFast:
                maxSpeed = 10.0f;
                acceleration = 25f;
                turnSpeedDegPerSec = 1080f;
                sprintMultiplier = 2.5f;
                break;
        }
    }

    private void OnDisable()
    {
        CourtManager.Instance?.UnregisterAgent(this);
    }

    private void OnValidate()
    {
        // インスペクターでプリセットが変更された時に自動適用
        if (Application.isPlaying)
        {
            ApplySpeedPreset();
        }
    }

    private void DetermineAgentId()
    {
        // 名前からエージェントIDを決定
        string name = gameObject.name.ToLower();
        if (name.Contains("capsule-w-1")) _agentId = 1;
        else if (name.Contains("capsule-w-2")) _agentId = 2;
        else if (name.Contains("capsule-w-3")) _agentId = 3;
        else if (name.Contains("capsule-b-1")) _agentId = 4;
        else if (name.Contains("capsule-b-2")) _agentId = 5;
        else if (name.Contains("capsule-b-3")) _agentId = 6;
        else _agentId = 0;
    }

    private void InitializeFixedRandom()
    {
        // エージェント固有のシードを設定
        int agentSeed = seed + _agentId * 1000;

        // 固定値の初期化（エージェントごとに異なるが再現可能）
        _wanderOffsetX = GetFixedRandom(agentSeed, 0.1f, 0.9f);
        _wanderOffsetZ = GetFixedRandom(agentSeed + 1, 0.1f, 0.9f);
        _idleThreshold = GetFixedRandom(agentSeed + 2, 0.0f, 1.0f);
        _directionChangeThreshold = GetFixedRandom(agentSeed + 3, 0.0f, 1.0f);
        _sprintThreshold = GetFixedRandom(agentSeed + 4, 0.0f, 1.0f);
        _sprintDuration = GetFixedRandom(agentSeed + 5, 0.5f, 2.0f);
    }

    private float GetFixedRandom(int localSeed, float min, float max)
    {
        // 固定ランダム値生成
        System.Random rand = new System.Random(localSeed);
        return min + (float)rand.NextDouble() * (max - min);
    }

    private void DetermineTeam()
    {
        if (gameObject.name.ToLower().Contains("capsule-w"))
        {
            _teamType = "white";
        }
        else if (gameObject.name.ToLower().Contains("capsule-b"))
        {
            _teamType = "black";
        }
        else
        {
            _teamType = "neutral";
        }
    }

    private void Update()
    {
        // エンターキーで全エージェントの一時停止/再開
        if (enablePauseControl && Input.GetKeyDown(KeyCode.Return))
        {
            ToggleGlobalPause();
        }

        // 一時停止状態の管理
        if (_isPaused)
        {
            _velocity = Vector3.zero;
            _cc.enabled = false;
            return; // 一時停止中は何もしない
        }

        // 凍結状態（外力を無視して動かさない）
        if (Time.time < _freezeUntil)
        {
            _velocity = Vector3.zero;
            if (!_cc.enabled) _cc.enabled = true;
            return;
        }

        // CharacterControllerを有効化
        if (!_cc.enabled)
        {
            _cc.enabled = true;
        }

        // CourtManager のグローバル設定を反映
        if (CourtManager.Instance != null && CourtManager.Instance.enableGlobalHeightSettings)
        {
            heightChangeSpeed = Mathf.Clamp(
                CourtManager.Instance.globalHeightChangeSpeed,
                heightChangeSpeedRange.x,
                heightChangeSpeedRange.y
            );
        }

        // 速度のキー入力調整は削除（インスペクター/グローバル設定からのみ変更）

        // 人間らしい行動パターン
        UpdateHumanBehavior();

        // ローミング目標の更新
        UpdateRoaming();

        // 境界チェックを最初に行う
        Vector3 boundaryForce = ComputeBoundaryPush();
        bool isOutOfBounds = boundaryForce.magnitude > boundaryPushStrength * 0.5f;

        Vector3 desired = Vector3.zero;

        if (!_isIdle)
        {
            desired += ComputeWander();
        }
        // ローミングのシーク力（常時弱めに加える）
        desired += ComputeRoamSeek();
        desired += ComputeSeparation();
        desired += ComputeAvoidance();

        // 境界維持を最優先
        desired += boundaryForce;

        // 境界外の場合は他の力を制限
        if (isOutOfBounds)
        {
            desired = Vector3.ClampMagnitude(desired, maxSpeed * 0.5f);

            // ログ出力は抑制
        }
        else
        {
            desired += ComputeWallAvoidance();

            // チーム関連の力を条件付きで適用
            if (!disableTeamForces)
            {
                desired += ComputeTeamCohesion();
                desired += ComputeTeamFormation();
                desired += ComputeOpponentAvoidance();
            }
            desired += ComputeTeamMixing(); // 混在促進は常に有効

            // パスカット機能
            if (enablePassCut)
            {
                desired += ComputePassCut();
            }
        }

        // ダッシュ中は速度を上げる（境界外の場合は制限）
        if (_isSprinting && !isOutOfBounds)
        {
            desired *= sprintMultiplier;
        }

        // 2D 平面上で動く (Y は固定)
        desired.y = 0f;

        // 速度更新
        Vector3 desiredVelocity = Vector3.ClampMagnitude(desired, maxSpeed);
        _velocity = Vector3.MoveTowards(_velocity, desiredVelocity, acceleration * Time.deltaTime);

        // 回転
        Vector3 moveDir = _velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : transform.forward;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDegPerSec * Time.deltaTime);
        }

        // 重力はオフ相当で地面に貼り付く
        Vector3 delta = _velocity * Time.deltaTime;
        _cc.Move(delta);

        // 高さ変化の適用（移動処理後にスケールを更新）
        if (enableHeightVariation)
        {
            // 直前まで無効だった→有効になった直後はスムーズ復帰を開始
            if (!_heightWasEnabled)
            {
                _heightResumeBlendUntil = Time.time + Mathf.Max(0f, heightResumeBlendDuration);
                float denom = Mathf.Max(0.0001f, _initialScale.y);
                _heightResumeStartFactor = transform.localScale.y / denom;
            }
            ApplyHeightVariation();
        }

        // 次フレーム用に状態を保持
        _heightWasEnabled = enableHeightVariation;
    }

    private void ApplyHeightVariation()
    {
        // サイン波で 0.5 ～ 1.0 の係数を生成（初期値を上限とする）
        float phase = Time.time * heightChangeSpeed + _agentId;
        float targetFactor = 0.75f + 0.25f * Mathf.Sin(phase);
        targetFactor = Mathf.Clamp(targetFactor, 0.5f, 1.0f);

        // 再開直後の滑らかな復帰
        float factor = targetFactor;
        if (Time.time < _heightResumeBlendUntil)
        {
            float total = Mathf.Max(0.0001f, heightResumeBlendDuration);
            float t = 1f - ((_heightResumeBlendUntil - Time.time) / total);
            t = Mathf.Clamp01(t);
            factor = Mathf.Lerp(_heightResumeStartFactor, targetFactor, t);
        }

        // 見た目のカプセルスケール更新（Y のみ変化）
        Vector3 targetScale = new Vector3(_initialScale.x, _initialScale.y * factor, _initialScale.z);
        transform.localScale = targetScale;

        // CharacterController も同期（高さとセンターY を線形スケール）
        if (_cc != null)
        {
            _cc.height = _initialCCHeight * factor;
            Vector3 c = _cc.center;
            c.y = _initialCCCenterY * factor;
            _cc.center = c;
        }
    }

    // 内部: 高さ変化のスムーズ復帰用
    private float _heightResumeBlendUntil = 0f;
    private float _heightResumeStartFactor = 1f;
    private bool _heightWasEnabled = true;

    // 高さ変化速度の外部設定用API
    public void SetHeightChangeSpeed(float newSpeed)
    {
        heightChangeSpeed = Mathf.Clamp(newSpeed, heightChangeSpeedRange.x, heightChangeSpeedRange.y);
    }

    public float GetHeightChangeSpeed()
    {
        return heightChangeSpeed;
    }

    private Vector3 ComputeWander()
    {
        if (useFixedRandom)
        {
            // 固定ランダムを使用したワンダー
            float time = Time.time;
            float jitterX = Mathf.Sin(time * 0.5f + _wanderOffsetX) * wanderJitter;
            float jitterZ = Mathf.Cos(time * 0.7f + _wanderOffsetZ) * wanderJitter;

            _wanderTarget += new Vector3(jitterX, 0f, jitterZ) * Time.deltaTime;
            _wanderTarget = transform.position + (transform.forward * wanderDistance) + (_wanderTarget - transform.position).normalized * wanderRadius;
        }
        else
        {
            // 元のランダムワンダー
            _wanderTarget += new Vector3(Random.Range(-1f, 1f) * wanderJitter, 0f, Random.Range(-1f, 1f) * wanderJitter);
            _wanderTarget = transform.position + (transform.forward * wanderDistance) + (_wanderTarget - transform.position).normalized * wanderRadius;
        }

        // ワンダーターゲットを境界内に制限
        _wanderTarget = ClampPositionToBounds(_wanderTarget);

        Vector3 steering = (_wanderTarget - transform.position);
        return steering;
    }

    private Vector3 ClampPositionToBounds(Vector3 position)
    {
        if (CourtManager.Instance == null) return position;
        if (!CourtManager.Instance.TryGetFloorBounds(out var bounds)) return position;

        // 境界内に制限（Z軸方向に追加パディング）
        float minX = bounds.min.x + boundaryPadding;
        float maxX = bounds.max.x - boundaryPadding;
        float minZ = bounds.min.z + boundaryPadding + zBoundaryExtraPadding;
        float maxZ = bounds.max.z - boundaryPadding;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);
        position.y = transform.position.y; // Y座標は現在位置を維持

        return position;
    }

    private void ResetRoamTimer()
    {
        _roamTimer = Random.Range(roamIntervalRange.x, roamIntervalRange.y);
    }

    private void PickNewRoamTarget()
    {
        if (CourtManager.Instance != null && CourtManager.Instance.TryGetFloorBounds(out var bounds))
        {
            float minX = bounds.min.x + boundaryPadding;
            float maxX = bounds.max.x - boundaryPadding;
            float minZ = bounds.min.z + boundaryPadding + zBoundaryExtraPadding;
            float maxZ = bounds.max.z - boundaryPadding;
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);
            _roamTarget = new Vector3(x, transform.position.y, z);
        }
        else
        {
            // 境界が取れない場合は前方遠め
            _roamTarget = transform.position + transform.forward * 6f;
            _roamTarget.y = transform.position.y;
        }
    }

    private void UpdateRoaming()
    {
        if (!enableRoam) return;
        _roamTimer -= Time.deltaTime;
        float dist = Vector3.Distance(transform.position, _roamTarget);
        if (_roamTimer <= 0f || dist < 0.6f)
        {
            ResetRoamTimer();
            PickNewRoamTarget();
        }
    }

    private Vector3 ComputeRoamSeek()
    {
        if (!enableRoam) return Vector3.zero;
        Vector3 toTarget = _roamTarget - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return Vector3.zero;
        return toTarget.normalized * roamSeekStrength;
    }

    private Vector3 ComputeSeparation()
    {
        if (CourtManager.Instance == null) return Vector3.zero;
        Vector3 force = Vector3.zero;
        int count = 0;
        foreach (var other in CourtManager.Instance.agents)
        {
            if (other == null || other == this) continue;
            Vector3 toMe = transform.position - other.transform.position;
            float dist = toMe.magnitude;
            if (dist > 0.0001f && dist < separationRadius)
            {
                force += toMe.normalized * (1f - dist / separationRadius);
                count++;
            }
        }
        if (count > 0)
        {
            force /= count;
        }
        return force * separationStrength;
    }

    private Vector3 ComputeAvoidance()
    {
        if (CourtManager.Instance == null) return Vector3.zero;
        Vector3 force = Vector3.zero;
        int count = 0;
        foreach (var other in CourtManager.Instance.agents)
        {
            if (other == null || other == this) continue;
            Vector3 toMe = transform.position - other.transform.position;
            float dist = toMe.magnitude;
            if (dist > 0.0001f && dist < avoidanceRadius)
            {
                Vector3 away = toMe.normalized;
                // 進行方向に対して側方へもずらすことで回避らしさを出す
                Vector3 side = Vector3.Cross(Vector3.up, (_velocity.sqrMagnitude > 0.01f ? _velocity.normalized : transform.forward));
                away = (away + side * 0.5f).normalized;
                force += away * (1f - dist / avoidanceRadius);
                count++;
            }
        }
        if (count > 0)
        {
            force /= count;
        }
        return force * avoidanceStrength;
    }

    private Vector3 ComputeBoundaryPush()
    {
        if (CourtManager.Instance == null) return Vector3.zero;
        if (!CourtManager.Instance.TryGetFloorBounds(out var bounds)) return Vector3.zero;

        Vector3 pos = transform.position;
        Vector3 velocity = _velocity;

        // XZ 平面で floor の内側に押し戻す（Z軸方向に追加パディング）
        float minX = bounds.min.x + boundaryPadding;
        float maxX = bounds.max.x - boundaryPadding;
        float minZ = bounds.min.z + boundaryPadding + zBoundaryExtraPadding; // Z軸方向の追加パディング
        float maxZ = bounds.max.z - boundaryPadding;

        Vector3 push = Vector3.zero;
        float maxPushDistance = 0f;

        // 現在位置の境界チェック
        if (pos.x < minX)
        {
            float distance = minX - pos.x;
            push.x += distance;
            maxPushDistance = Mathf.Max(maxPushDistance, distance);
        }
        if (pos.x > maxX)
        {
            float distance = pos.x - maxX;
            push.x -= distance;
            maxPushDistance = Mathf.Max(maxPushDistance, distance);
        }
        if (pos.z < minZ)
        {
            float distance = minZ - pos.z;
            push.z += distance;
            maxPushDistance = Mathf.Max(maxPushDistance, distance);
        }
        if (pos.z > maxZ)
        {
            float distance = pos.z - maxZ;
            push.z -= distance;
            maxPushDistance = Mathf.Max(maxPushDistance, distance);
        }

        // 予測的境界チェック（速度ベース）
        if (velocity.magnitude > 0.1f)
        {
            Vector3 predictedPos = pos + velocity.normalized * predictiveBoundaryDistance;

            if (predictedPos.x < minX)
            {
                float distance = minX - predictedPos.x;
                push.x += distance * 0.5f; // 予測的な押し戻しは軽め
                maxPushDistance = Mathf.Max(maxPushDistance, distance);
            }
            if (predictedPos.x > maxX)
            {
                float distance = predictedPos.x - maxX;
                push.x -= distance * 0.5f;
                maxPushDistance = Mathf.Max(maxPushDistance, distance);
            }
            if (predictedPos.z < minZ)
            {
                float distance = minZ - predictedPos.z;
                push.z += distance * 0.8f; // Z軸方向の予測は強め
                maxPushDistance = Mathf.Max(maxPushDistance, distance);
            }
            if (predictedPos.z > maxZ)
            {
                float distance = predictedPos.z - maxZ;
                push.z -= distance * 0.5f;
                maxPushDistance = Mathf.Max(maxPushDistance, distance);
            }
        }

        // 境界を超えている場合は強力に押し戻す
        if (maxPushDistance > 0f)
        {
            float forceStrength = Mathf.Min(boundaryPushStrength * boundaryForceMultiplier * maxPushDistance, maxBoundaryForce);
            push = push.normalized * forceStrength;

            // ログ出力は抑制
        }
        else
        {
            // 境界に近い場合は軽い押し戻し
            push = push.normalized * boundaryPushStrength;
        }

        return push;
    }

    private Vector3 ComputeWallAvoidance()
    {
        if (CourtManager.Instance == null) return Vector3.zero;
        var walls = CourtManager.Instance.wallColliders;
        if (walls == null || walls.Count == 0) return Vector3.zero;

        Vector3 avoid = Vector3.zero;
        int count = 0;
        float closestWallDist = float.MaxValue;
        Vector3 closestWallPoint = Vector3.zero;

        foreach (var col in walls)
        {
            if (col == null) continue;
            Vector3 closest = col.ClosestPoint(transform.position);
            Vector3 toMe = transform.position - closest;
            float dist = toMe.magnitude;
            if (dist < closestWallDist)
            {
                closestWallDist = dist;
                closestWallPoint = closest;
            }
            if (dist < wallDetectDistance && dist > 0.0001f)
            {
                // 壁の法線方向に強く押し返す
                Vector3 away = toMe.normalized;
                float strength = (1f - dist / wallDetectDistance) * wallAvoidStrength;

                // 壁に平行な方向への移動を促進
                Vector3 wallTangent = Vector3.Cross(Vector3.up, toMe.normalized);
                Vector3 currentVel = _velocity.sqrMagnitude > 0.01f ? _velocity.normalized : transform.forward;

                // 壁に沿って移動する方向を計算
                float dot = Vector3.Dot(currentVel, wallTangent);
                Vector3 slideDirection = wallTangent * Mathf.Sign(dot);

                // 壁から離れる力 + 壁に沿って移動する力
                avoid += away * strength + slideDirection * strength * 0.6f;
                count++;
            }
        }

        // 壁が非常に近い場合は強制的に方向転換
        if (closestWallDist < 0.3f)
        {
            Vector3 awayFromWall = (transform.position - closestWallPoint).normalized;
            Vector3 randomDirection;
            if (useFixedRandom)
            {
                // 固定ランダム方向
                float angle = (Time.time * 2f + _agentId * 60f) % 360f;
                randomDirection = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
            }
            else
            {
                randomDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            }
            Vector3 escapeDirection = (awayFromWall + randomDirection * 0.5f).normalized;
            avoid += escapeDirection * wallAvoidStrength * 2f;
        }

        return avoid;
    }

    private Vector3 ComputeTeamCohesion()
    {
        if (CourtManager.Instance == null || _teamType == "neutral") return Vector3.zero;

        Vector3 teamCenter = Vector3.zero;
        int teamCount = 0;

        // チームメイトの中心位置を計算
        foreach (var other in CourtManager.Instance.agents)
        {
            if (other == null || other == this) continue;
            if (other._teamType == _teamType)
            {
                teamCenter += other.transform.position;
                teamCount++;
            }
        }

        if (teamCount > 0)
        {
            teamCenter /= teamCount;
            _teamCenter = teamCenter;

            // チーム中心に向かう力（距離に応じて弱くなる）
            Vector3 toCenter = teamCenter - transform.position;
            float dist = toCenter.magnitude;
            if (dist > teamCohesionRadius)
            {
                // 距離が遠いほど弱い力
                float strength = Mathf.Clamp01((dist - teamCohesionRadius) / teamCohesionRadius);
                return toCenter.normalized * teamCohesionStrength * strength * 0.5f;
            }
        }

        return Vector3.zero;
    }

    private Vector3 ComputeTeamFormation()
    {
        if (CourtManager.Instance == null || _teamType == "neutral") return Vector3.zero;

        Vector3 formationForce = Vector3.zero;
        int teamCount = 0;

        // チームメイトとの適切な距離を保つ（より柔軟に）
        foreach (var other in CourtManager.Instance.agents)
        {
            if (other == null || other == this) continue;
            if (other._teamType == _teamType)
            {
                Vector3 toOther = other.transform.position - transform.position;
                float dist = toOther.magnitude;

                // より広い適切な距離（1.5-5.0）を保つ
                if (dist < 1.5f)
                {
                    formationForce += -toOther.normalized * (1.5f - dist) * 0.5f;
                }
                else if (dist > 5.0f)
                {
                    formationForce += toOther.normalized * (dist - 5.0f) * 0.3f;
                }
                teamCount++;
            }
        }

        if (teamCount > 0)
        {
            formationForce /= teamCount;
        }

        return formationForce * teamFormationStrength;
    }

    private Vector3 ComputeOpponentAvoidance()
    {
        if (CourtManager.Instance == null || _teamType == "neutral") return Vector3.zero;

        Vector3 avoidForce = Vector3.zero;
        int opponentCount = 0;

        // 相手チームを回避
        foreach (var other in CourtManager.Instance.agents)
        {
            if (other == null || other == this) continue;
            if (other._teamType != _teamType && other._teamType != "neutral")
            {
                Vector3 toMe = transform.position - other.transform.position;
                float dist = toMe.magnitude;

                if (dist < opponentAvoidanceRadius && dist > 0.0001f)
                {
                    Vector3 away = toMe.normalized;
                    float strength = (1f - dist / opponentAvoidanceRadius) * opponentAvoidanceStrength;
                    avoidForce += away * strength;
                    opponentCount++;
                }
            }
        }

        if (opponentCount > 0)
        {
            avoidForce /= opponentCount;
        }

        return avoidForce;
    }

    private Vector3 ComputeTeamMixing()
    {
        if (CourtManager.Instance == null || _teamType == "neutral") return Vector3.zero;

        Vector3 mixingForce = Vector3.zero;
        int opponentCount = 0;

        // 相手チームのプレイヤーに向かう力（混在を強力に促進）
        foreach (var other in CourtManager.Instance.agents)
        {
            if (other == null || other == this) continue;
            if (other._teamType != _teamType && other._teamType != "neutral")
            {
                Vector3 toOpponent = other.transform.position - transform.position;
                float dist = toOpponent.magnitude;

                // より広い距離範囲（2.0-8.0）で相手に向かう力
                if (dist > 2.0f && dist < 8.0f)
                {
                    Vector3 towardOpponent = toOpponent.normalized;
                    float strength = (dist - 2.0f) / 6.0f; // 距離に応じた強さ
                    mixingForce += towardOpponent * strength * teamMixingStrength;
                    opponentCount++;
                }
            }
        }

        if (opponentCount > 0)
        {
            mixingForce /= opponentCount;
        }

        return mixingForce;
    }

    private Vector3 ComputePassCut()
    {
        if (CourtManager.Instance == null) return Vector3.zero;

        // ボールを取得
        var ball = FindObjectOfType<BallPassController>();
        if (ball == null) return Vector3.zero;

        // ボールが動いていない場合はパスカットしない
        if (!ball.IsMoving()) return Vector3.zero;

        // ボールの現在位置と目標位置を取得
        Vector3 ballPos = ball.transform.position;
        Vector3 ballTarget = ball.GetCurrentTargetPosition();

        // ボールの移動方向を計算
        Vector3 ballDirection = (ballTarget - ballPos).normalized;
        float ballDistance = Vector3.Distance(ballPos, ballTarget);

        // ボールの移動速度を推定
        float ballSpeed = ball.GetCurrentSpeed();
        float timeToTarget = ballDistance / ballSpeed;

        // パスカットの確率チェック
        bool shouldAttemptCut = false;
        if (useFixedRandom)
        {
            float cutCheck = Mathf.Sin(Time.time * 0.8f + _agentId * 0.3f) * 0.5f + 0.5f;
            shouldAttemptCut = cutCheck < passCutChance;
        }
        else
        {
            shouldAttemptCut = Random.Range(0f, 1f) < passCutChance;
        }

        if (!shouldAttemptCut) return Vector3.zero;

        // パスカット可能な位置を計算
        Vector3 cutPosition = ballPos + ballDirection * (ballDistance * 0.5f); // パスの中間点
        Vector3 toCutPosition = cutPosition - transform.position;
        float distanceToCut = toCutPosition.magnitude;

        // パスカット可能な距離内かチェック
        if (distanceToCut > passCutDetectionRadius) return Vector3.zero;

        // パスカットの成功率を計算（距離と時間に基づく）
        float timeToCut = distanceToCut / maxSpeed;
        float cutSuccess = Mathf.Clamp01(1.0f - (timeToCut / timeToTarget));

        // 成功率が低い場合はパスカットしない
        if (cutSuccess < 0.3f) return Vector3.zero;

        // パスカット方向への力を計算
        Vector3 cutForce = toCutPosition.normalized * passCutStrength * cutSuccess;

        return cutForce;
    }

    private void UpdateHumanBehavior()
    {
        // アイドル状態の管理
        if (_isIdle)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f)
            {
                _isIdle = false;
                if (useFixedRandom)
                {
                    // 固定ランダム方向
                    float angle = (Time.time * 30f + _agentId * 60f) % 360f;
                    Vector3 randomDir = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                    _wanderTarget = transform.position + randomDir * wanderRadius;
                }
                else
                {
                    _wanderTarget = transform.position + Random.insideUnitSphere * wanderRadius;
                }
                _wanderTarget.y = 0f;
                _wanderTarget = ClampPositionToBounds(_wanderTarget);
            }
        }
        else
        {
            // ランダムに停止（固定ランダム）
            if (useFixedRandom)
            {
                float idleCheck = Mathf.Sin(Time.time * 0.5f + _agentId * 0.5f) * 0.5f + 0.5f;
                if (idleCheck < idleChance)
                {
                    _isIdle = true;
                    _idleTimer = 1f + _agentId * 0.3f; // エージェントごとに異なる停止時間
                }
            }
            else
            {
                if (Random.Range(0f, 1f) < idleChance * Time.deltaTime)
                {
                    _isIdle = true;
                    _idleTimer = Random.Range(1f, 3f);
                }
            }
        }

        // 方向転換
        _directionChangeTimer -= Time.deltaTime;
        if (_directionChangeTimer <= 0f)
        {
            bool shouldChangeDirection = false;
            if (useFixedRandom)
            {
                float directionCheck = Mathf.Sin(Time.time * 0.3f + _agentId * 0.7f) * 0.5f + 0.5f;
                shouldChangeDirection = directionCheck < directionChangeChance;
            }
            else
            {
                shouldChangeDirection = Random.Range(0f, 1f) < directionChangeChance;
            }

            if (shouldChangeDirection)
            {
                Vector3 randomDir;
                if (useFixedRandom)
                {
                    float angle = (Time.time * 45f + _agentId * 90f) % 360f;
                    randomDir = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
                }
                else
                {
                    randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                }
                _wanderTarget = transform.position + randomDir * wanderDistance;
                _directionChangeTimer = 2f + _agentId * 0.5f; // エージェントごとに異なる間隔
                _wanderTarget = ClampPositionToBounds(_wanderTarget);
            }
        }

        // ダッシュ判定
        if (!_isIdle)
        {
            bool shouldSprint = false;
            if (useFixedRandom)
            {
                float sprintCheck = Mathf.Sin(Time.time * 0.4f + _agentId * 0.6f) * 0.5f + 0.5f;
                shouldSprint = sprintCheck < sprintChance;
            }
            else
            {
                shouldSprint = Random.Range(0f, 1f) < sprintChance * Time.deltaTime;
            }

            if (shouldSprint)
            {
                _isSprinting = true;
                StartCoroutine(SprintCoroutine());
            }
        }
    }

    private System.Collections.IEnumerator SprintCoroutine()
    {
        float duration = useFixedRandom ? _sprintDuration : Random.Range(0.5f, 2f);
        yield return new WaitForSeconds(duration);
        _isSprinting = false;
    }

    // 一時停止/再開の切り替え
    public void TogglePause()
    {
        if (_isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    // 全エージェントの一時停止/再開切り替え
    public static void ToggleGlobalPause()
    {
        _globalPause = !_globalPause;
        BasketballAgentController[] agents = FindObjectsOfType<BasketballAgentController>();

        foreach (var agent in agents)
        {
            if (_globalPause)
            {
                agent.Pause();
            }
            else
            {
                agent.Resume();
            }
        }

        Debug.Log($"全エージェントを {(_globalPause ? "一時停止" : "再開")} しました。");

        // 再開時にパスも開始（パス回数をカウントしない）
        if (!_globalPause)
        {
            var pass = FindObjectOfType<BallPassController>();
            if (pass != null)
            {
                pass.ResumePassing();
            }
        }
    }

    // 全エージェントの一時停止
    public static void PauseAll()
    {
        _globalPause = true;
        BasketballAgentController[] agents = FindObjectsOfType<BasketballAgentController>();

        foreach (var agent in agents)
        {
            agent.Pause();
        }

        Debug.Log("全エージェントを一時停止しました。");
    }

    // 全エージェントの再開
    public static void ResumeAll()
    {
        _globalPause = false;
        BasketballAgentController[] agents = FindObjectsOfType<BasketballAgentController>();

        foreach (var agent in agents)
        {
            agent.Resume();
        }

        Debug.Log("全エージェントを再開しました。");

        // パスを開始（パス回数をカウントしない）
        var pass = FindObjectOfType<BallPassController>();
        if (pass != null)
        {
            pass.ResumePassing();
        }
    }

    // 一時停止
    public void Pause()
    {
        _isPaused = true;
        _velocity = Vector3.zero;
        _cc.enabled = false;
        Debug.Log($"{gameObject.name} を一時停止しました。");
    }

    // 再開
    public void Resume()
    {
        _isPaused = false;
        _cc.enabled = true;
        // ブースト機能は削除

        // 個別再開時にもパスを開始（パス回数をカウントしない）
        var pass = FindObjectOfType<BallPassController>();
        if (pass != null)
        {
            pass.ResumePassing();
        }
    }

    // ブースト関連コードは削除

    // 一時停止状態の取得
    public bool IsPaused()
    {
        return _isPaused;
    }

    // 現在の速度を取得（ボールの予測位置計算用）
    public Vector3 GetCurrentVelocity()
    {
        return _velocity;
    }

    // 現在の高さ変化係数を取得（ボールの着地点計算用）
    public float GetCurrentHeightFactor()
    {
        if (!enableHeightVariation)
        {
            return 1.0f;
        }

        // サイン波で 0.5 ～ 1.0 の係数を生成（初期値を上限とする）
        float phase = Time.time * heightChangeSpeed + _agentId;
        float factor = 0.75f + 0.25f * Mathf.Sin(phase);
        return Mathf.Clamp(factor, 0.5f, 1.0f);
    }

    // 静止状態を強制的に設定（ボールパス用）
    public void SetIdleState(bool idle, float duration = -1f)
    {
        _isIdle = idle;
        if (idle && duration > 0f)
        {
            _idleTimer = duration;
        }
        else if (!idle)
        {
            _idleTimer = 0f;
        }
    }

    // 完全に動きを止める（境界/回避等の外力も無視）
    public void FreezeFor(float duration)
    {
        _freezeUntil = Mathf.Max(_freezeUntil, Time.time + Mathf.Max(0f, duration));
        _velocity = Vector3.zero;
        if (_cc != null) _cc.enabled = true;
    }

}


