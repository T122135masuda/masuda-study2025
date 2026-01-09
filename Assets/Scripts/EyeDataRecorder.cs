using UnityEngine;
using UnityEngine.SceneManagement;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;

public class EyeDataRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    public string gazeDataFileName = "GazeData";
    public string pupilDataFileName = "PupilData";
    public string anchorDataFileName = "AnchorData"; // アンカーデータのファイル名
    [Range(30, 120)]
    public int targetRecordingFrequency = 120; // 目標記録周波数（Hz）

    private bool isRecording = false;
    private List<GazeData> gazeDataList = new List<GazeData>();
    private List<PupilData> pupilDataList = new List<PupilData>();
    private List<AnchorData> anchorDataList = new List<AnchorData>(); // アンカーデータのリスト

    private string dataFolderPath;
    private float recordingStartTime;
    private float nextRecordTime;
    private float recordInterval;
    private float lastStatusLogTime;
    private bool hasLoggedFirstData = false;
    // エンター押下で測定開始した時間
    private float measurementStartTime = -1f;
    // 自動終了コルーチン
    private Coroutine autoStopCoroutine = null;
    private const float AUTO_STOP_DURATION_SAMPLE_SCENE = 180.0f; // SampleScene: 180秒後に自動終了
    private const float AUTO_STOP_DURATION_SAMPLE_SCENE_1_2 = 180.0f; // SampleScene1/2: 180秒後に自動終了

    // 視線データ構造
    [System.Serializable]
    public struct GazeData
    {
        public float timestamp;
        public bool leftGazeValid;
        public Vector3 leftGazePosition;
        public Quaternion leftGazeRotation;
        public bool rightGazeValid;
        public Vector3 rightGazePosition;
        public Quaternion rightGazeRotation;
    }

    // 瞳孔データ構造
    [System.Serializable]
    public struct PupilData
    {
        public float timestamp;
        public bool leftDiameterValid;
        public float leftPupilDiameter;
        public bool leftPositionValid;
        public Vector2 leftPupilPosition;
        public bool rightDiameterValid;
        public float rightPupilDiameter;
        public bool rightPositionValid;
        public Vector2 rightPupilPosition;
    }

    // アンカーデータ構造
    [System.Serializable]
    public struct AnchorData
    {
        public float timestamp; // 記録開始からの相対時間
        public int anchorIndex; // アンカーの番号（1から開始）
    }

    void Start()
    {
        // Assets/dataフォルダのパスを設定
        dataFolderPath = @"C:\Users\vrdsl\Desktop\masuda-lab\masuda-study2025\Assets\data";

        // フォルダが存在しない場合は作成
        if (!Directory.Exists(dataFolderPath))
        {
            Directory.CreateDirectory(dataFolderPath);
            Debug.Log($"[EyeDataRecorder] データフォルダを作成しました: {dataFolderPath}");
        }
        else
        {
            Debug.Log($"[EyeDataRecorder] データフォルダを確認しました: {dataFolderPath}");
        }

        // 記録間隔を計算（秒）
        recordInterval = 1.0f / targetRecordingFrequency;

        Debug.Log($"[EyeDataRecorder] ===== 視線データ記録システムが初期化されました =====");
        Debug.Log($"[EyeDataRecorder] 目標記録周波数: {targetRecordingFrequency}Hz");
        Debug.Log($"[EyeDataRecorder] 記録間隔: {recordInterval:F6}秒");
        Debug.Log("[EyeDataRecorder] エンターキーを押して記録を開始/停止してください");
        Debug.Log("[EyeDataRecorder] スペースキーを押してアンカーを打てます（記録中のみ有効）");

        // アイトラッカーAPIのテスト呼び出し（初期化確認）
        try
        {
            XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] testGazes);
            if (testGazes != null && testGazes.Length >= 2)
            {
                Debug.Log($"[EyeDataRecorder] ✓ アイトラッカーAPI接続確認: 視線データ取得可能 (配列サイズ: {testGazes.Length})");
            }
            else
            {
                Debug.LogWarning($"[EyeDataRecorder] ⚠ アイトラッカーAPI接続確認: 視線データ配列が無効 (配列: {(testGazes == null ? "null" : testGazes.Length.ToString())})");
            }

            XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] testPupils);
            if (testPupils != null && testPupils.Length >= 2)
            {
                Debug.Log($"[EyeDataRecorder] ✓ アイトラッカーAPI接続確認: 瞳孔データ取得可能 (配列サイズ: {testPupils.Length})");
            }
            else
            {
                Debug.LogWarning($"[EyeDataRecorder] ⚠ アイトラッカーAPI接続確認: 瞳孔データ配列が無効 (配列: {(testPupils == null ? "null" : testPupils.Length.ToString())})");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeDataRecorder] ✗ アイトラッカーAPI接続確認でエラー: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    void Update()
    {
        // エンターキーの入力チェック
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!isRecording)
            {
                StartRecording();
            }
            else
            {
                // 自動終了するシーンの場合は、エンターキーでの終了は無効
                if (!RequiresAutoStop())
                {
                    StopRecording();
                }
            }
        }

        // スペースキーでアンカーを打つ（記録中のみ有効）
        if (isRecording && Input.GetKeyDown(KeyCode.Space))
        {
            RecordAnchor();
        }

        // 記録中の場合、指定された周波数でデータを収集
        if (isRecording && Time.time >= nextRecordTime)
        {
            // データ取得を試行
            RecordGazeData();
            RecordPupilData();
            nextRecordTime = Time.time + recordInterval;
        }

        // 記録中だがデータが取得できていない場合の警告
        if (isRecording)
        {
            float elapsedTime = Time.time - recordingStartTime;

            // 初回データ取得の確認（0.5秒後）
            if (elapsedTime > 0.5f && !hasLoggedFirstData)
            {
                Debug.LogWarning($"[EyeDataRecorder] ⚠ 記録開始から0.5秒経過しましたが、まだ初回データが取得できていません。");
            }

            // データが取得できていない場合の警告（5秒ごと）
            if (elapsedTime > 5.0f && gazeDataList.Count == 0 && pupilDataList.Count == 0)
            {
                if (Time.time - lastStatusLogTime >= 5.0f)
                {
                    Debug.LogWarning($"[EyeDataRecorder] ⚠ 記録開始から{elapsedTime:F1}秒経過しましたが、データが取得できていません。");
                    Debug.LogWarning($"[EyeDataRecorder] アイトラッカーが接続されているか、VRヘッドセットが正しく動作しているか確認してください。");
                    lastStatusLogTime = Time.time;
                }
            }
        }
    }

    void StartRecording()
    {
        isRecording = true;
        gazeDataList.Clear();
        pupilDataList.Clear();
        anchorDataList.Clear(); // アンカーデータもクリア
        recordingStartTime = Time.time; // 記録開始時刻を設定
        nextRecordTime = Time.time; // 次の記録時刻を初期化
        lastStatusLogTime = Time.time; // ステータスログの時刻を初期化
        hasLoggedFirstData = false; // 初回データログフラグをリセット
        measurementStartTime = Time.time; // エンター押下時刻を記録

        // 自動終了が必要なシーンの場合は、指定秒数後に自動終了するコルーチンを開始
        if (RequiresAutoStop())
        {
            float autoStopDuration = GetAutoStopDuration();
            if (autoStopCoroutine != null)
            {
                StopCoroutine(autoStopCoroutine);
            }
            autoStopCoroutine = StartCoroutine(AutoStopAfterDelay(autoStopDuration));
            string sceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[EyeDataRecorder] ===== データ記録を開始しました（{targetRecordingFrequency}Hz） =====");
            Debug.Log($"[EyeDataRecorder] {sceneName}: {autoStopDuration}秒後に自動終了します");
            Debug.Log($"[EyeDataRecorder] 視線データ・瞳孔データの取得を開始します...");
        }
        else
        {
            Debug.Log($"[EyeDataRecorder] ===== データ記録を開始しました（{targetRecordingFrequency}Hz） =====");
            Debug.Log($"[EyeDataRecorder] 視線データ・瞳孔データの取得を開始します...");
            Debug.Log("[EyeDataRecorder] エンターキーを押して記録を停止してください");
        }
    }

    void StopRecording()
    {
        isRecording = false;

        // 自動終了コルーチンを停止
        if (autoStopCoroutine != null)
        {
            StopCoroutine(autoStopCoroutine);
            autoStopCoroutine = null;
        }

        // CSVファイルに保存（個別ファイル）
        SaveGazeDataToCSV();
        SavePupilDataToCSV();
        SaveAnchorDataToCSV(); // アンカーデータも保存

        // 統合CSVファイルに保存
        SaveUnifiedDataToCSV();

        float recordingDuration = Time.time - recordingStartTime;
        float actualFrequency = gazeDataList.Count / recordingDuration;

        Debug.Log("[EyeDataRecorder] データ記録を終了しました");
        Debug.Log($"[EyeDataRecorder] 記録時間: {recordingDuration:F2}秒");
        Debug.Log($"[EyeDataRecorder] 視線データ: {gazeDataList.Count}件");
        Debug.Log($"[EyeDataRecorder] 瞳孔データ: {pupilDataList.Count}件");
        Debug.Log($"[EyeDataRecorder] アンカーデータ: {anchorDataList.Count}件");
        Debug.Log($"[EyeDataRecorder] 実際の記録周波数: {actualFrequency:F1}Hz");
    }

    /// <summary>
    /// 現在のシーンがSampleSceneかどうかを判定
    /// </summary>
    private bool IsSampleScene()
    {
        return SceneManager.GetActiveScene().name == "SampleScene";
    }

    /// <summary>
    /// 現在のシーンがSampleScene1かどうかを判定
    /// </summary>
    private bool IsSampleScene1()
    {
        return SceneManager.GetActiveScene().name == "SampleScene1";
    }

    /// <summary>
    /// 現在のシーンがSampleScene2かどうかを判定
    /// </summary>
    private bool IsSampleScene2()
    {
        return SceneManager.GetActiveScene().name == "SampleScene2";
    }

    /// <summary>
    /// 現在のシーンが自動終了を必要とするかどうかを判定
    /// </summary>
    private bool RequiresAutoStop()
    {
        return IsSampleScene() || IsSampleScene1() || IsSampleScene2();
    }

    /// <summary>
    /// 現在のシーンに応じた自動終了時間を取得
    /// </summary>
    private float GetAutoStopDuration()
    {
        if (IsSampleScene())
        {
            return AUTO_STOP_DURATION_SAMPLE_SCENE;
        }
        else if (IsSampleScene1() || IsSampleScene2())
        {
            return AUTO_STOP_DURATION_SAMPLE_SCENE_1_2;
        }
        return 0f; // 自動終了しないシーンの場合
    }

    /// <summary>
    /// 指定秒数後に自動で記録を終了するコルーチン
    /// </summary>
    private IEnumerator AutoStopAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (isRecording)
        {
            Debug.Log($"[EyeDataRecorder] {delay}秒経過しました。自動で記録を終了します。");
            StopRecording();
        }
    }

    void RecordGazeData()
    {
        try
        {
            // API呼び出しを試行
            XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] out_gazes);

            if (out_gazes == null || out_gazes.Length < 2)
            {
                Debug.LogWarning($"[EyeDataRecorder] 視線データ配列が無効です (配列: {(out_gazes == null ? "null" : out_gazes.Length.ToString())})");
                return;
            }

            XrSingleEyeGazeDataHTC leftGaze = out_gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
            XrSingleEyeGazeDataHTC rightGaze = out_gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];

            GazeData data = new GazeData
            {
                timestamp = Time.time - recordingStartTime,
                leftGazeValid = leftGaze.isValid,
                leftGazePosition = leftGaze.isValid ? leftGaze.gazePose.position.ToUnityVector() : Vector3.zero,
                leftGazeRotation = leftGaze.isValid ? leftGaze.gazePose.orientation.ToUnityQuaternion() : Quaternion.identity,
                rightGazeValid = rightGaze.isValid,
                rightGazePosition = rightGaze.isValid ? rightGaze.gazePose.position.ToUnityVector() : Vector3.zero,
                rightGazeRotation = rightGaze.isValid ? rightGaze.gazePose.orientation.ToUnityQuaternion() : Quaternion.identity
            };

            gazeDataList.Add(data);

            // 初回データ取得時にログ出力
            if (!hasLoggedFirstData)
            {
                Debug.Log($"[EyeDataRecorder] ✓ 視線データ取得開始 - 左目: {(leftGaze.isValid ? "有効" : "無効")}, 右目: {(rightGaze.isValid ? "有効" : "無効")}");

                // 瞳孔データも取得済みの場合、その情報も出力
                if (pupilDataList.Count > 0)
                {
                    var lastPupilData = pupilDataList[pupilDataList.Count - 1];
                    Debug.Log($"[EyeDataRecorder] ✓ 瞳孔データ取得開始 - 左目直径: {(lastPupilData.leftDiameterValid ? "有効" : "無効")}, 右目直径: {(lastPupilData.rightDiameterValid ? "有効" : "無効")}");
                }

                hasLoggedFirstData = true;
            }

            // 1秒ごとにデータ取得状況をログ出力
            if (Time.time - lastStatusLogTime >= 1.0f)
            {
                Debug.Log($"[EyeDataRecorder] 視線データ取得中... 左目: {(leftGaze.isValid ? "✓有効" : "✗無効")}, 右目: {(rightGaze.isValid ? "✓有効" : "✗無効")} (取得済み: {gazeDataList.Count}件)");

                // 瞳孔データの状況も同時に出力（最新のデータを取得）
                if (pupilDataList.Count > 0)
                {
                    var lastPupilData = pupilDataList[pupilDataList.Count - 1];
                    Debug.Log($"[EyeDataRecorder] 瞳孔データ取得中... 左目直径: {(lastPupilData.leftDiameterValid ? "✓有効" : "✗無効")}, 右目直径: {(lastPupilData.rightDiameterValid ? "✓有効" : "✗無効")} (取得済み: {pupilDataList.Count}件)");
                }

                lastStatusLogTime = Time.time;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeDataRecorder] ✗ 視線データの取得中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    void RecordPupilData()
    {
        try
        {
            // API呼び出しを試行
            XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] pupils);

            if (pupils == null || pupils.Length < 2)
            {
                Debug.LogWarning($"[EyeDataRecorder] 瞳孔データ配列が無効です (配列: {(pupils == null ? "null" : pupils.Length.ToString())})");
                return;
            }

            var leftPupil = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
            var rightPupil = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];

            PupilData data = new PupilData
            {
                timestamp = Time.time - recordingStartTime,
                leftDiameterValid = leftPupil.isDiameterValid,
                leftPupilDiameter = leftPupil.isDiameterValid ? leftPupil.pupilDiameter : 0f,
                leftPositionValid = leftPupil.isPositionValid,
                leftPupilPosition = leftPupil.isPositionValid ?
              new Vector2(leftPupil.pupilPosition.x, leftPupil.pupilPosition.y) : Vector2.zero,
                rightDiameterValid = rightPupil.isDiameterValid,
                rightPupilDiameter = rightPupil.isDiameterValid ? rightPupil.pupilDiameter : 0f,
                rightPositionValid = rightPupil.isPositionValid,
                rightPupilPosition = rightPupil.isPositionValid ?
              new Vector2(rightPupil.pupilPosition.x, rightPupil.pupilPosition.y) : Vector2.zero
            };

            pupilDataList.Add(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeDataRecorder] ✗ 瞳孔データの取得中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    void SaveGazeDataToCSV()
    {
        // データがなくても空のCSVファイルを出力する
        bool hasData = gazeDataList.Count > 0;
        if (!hasData)
        {
            Debug.LogWarning("[EyeDataRecorder] 視線データがありません（空のCSVファイルを出力します）");
        }

        try
        {
            // フォルダが存在しない場合は再作成
            if (!Directory.Exists(dataFolderPath))
            {
                Directory.CreateDirectory(dataFolderPath);
                Debug.Log($"[EyeDataRecorder] データフォルダを再作成しました: {dataFolderPath}");
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{gazeDataFileName}_{timestamp}.csv";
            string filePath = Path.Combine(dataFolderPath, fileName).Replace('/', Path.DirectorySeparatorChar);

            Debug.Log($"[EyeDataRecorder] 視線データの保存を開始します... パス: {filePath}");

            StringBuilder csv = new StringBuilder();

            // ヘッダー行
            csv.AppendLine("Timestamp,LeftGazeValid,LeftGazePosX,LeftGazePosY,LeftGazePosZ,LeftGazeRotX,LeftGazeRotY,LeftGazeRotZ,LeftGazeRotW,RightGazeValid,RightGazePosX,RightGazePosY,RightGazePosZ,RightGazeRotX,RightGazeRotY,RightGazeRotZ,RightGazeRotW");

            // データ行（データがない場合は空行のみ）
            if (hasData)
            {
                foreach (var data in gazeDataList)
                {
                    csv.AppendLine($"{data.timestamp:F3}," +
                                  $"{data.leftGazeValid}," +
                                  $"{data.leftGazePosition.x:F6},{data.leftGazePosition.y:F6},{data.leftGazePosition.z:F6}," +
                                  $"{data.leftGazeRotation.x:F6},{data.leftGazeRotation.y:F6},{data.leftGazeRotation.z:F6},{data.leftGazeRotation.w:F6}," +
                                  $"{data.rightGazeValid}," +
                                  $"{data.rightGazePosition.x:F6},{data.rightGazePosition.y:F6},{data.rightGazePosition.z:F6}," +
                                  $"{data.rightGazeRotation.x:F6},{data.rightGazeRotation.y:F6},{data.rightGazeRotation.z:F6},{data.rightGazeRotation.w:F6}");
                }
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            // ファイルが実際に存在するか確認
            if (File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                int dataCount = hasData ? gazeDataList.Count : 0;
                Debug.Log($"[EyeDataRecorder] ✓ 視線データを保存しました: {filePath} (サイズ: {fileSize} bytes, データ件数: {dataCount}件)");
            }
            else
            {
                Debug.LogError($"[EyeDataRecorder] ✗ ファイルの保存に失敗しました: {filePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeDataRecorder] ✗ 視線データの保存中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    void SavePupilDataToCSV()
    {
        // データがなくても空のCSVファイルを出力する
        bool hasData = pupilDataList.Count > 0;
        if (!hasData)
        {
            Debug.LogWarning("[EyeDataRecorder] 瞳孔データがありません（空のCSVファイルを出力します）");
        }

        try
        {
            // フォルダが存在しない場合は再作成
            if (!Directory.Exists(dataFolderPath))
            {
                Directory.CreateDirectory(dataFolderPath);
                Debug.Log($"[EyeDataRecorder] データフォルダを再作成しました: {dataFolderPath}");
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{pupilDataFileName}_{timestamp}.csv";
            string filePath = Path.Combine(dataFolderPath, fileName).Replace('/', Path.DirectorySeparatorChar);

            Debug.Log($"[EyeDataRecorder] 瞳孔データの保存を開始します... パス: {filePath}");

            StringBuilder csv = new StringBuilder();

            // ヘッダー行
            csv.AppendLine("Timestamp,LeftDiameterValid,LeftPupilDiameter,LeftPositionValid,LeftPupilPosX,LeftPupilPosY,RightDiameterValid,RightPupilDiameter,RightPositionValid,RightPupilPosX,RightPupilPosY");

            // データ行（データがない場合は空行のみ）
            if (hasData)
            {
                foreach (var data in pupilDataList)
                {
                    csv.AppendLine($"{data.timestamp:F3}," +
                                  $"{data.leftDiameterValid}," +
                                  $"{data.leftPupilDiameter:F3}," +
                                  $"{data.leftPositionValid}," +
                                  $"{data.leftPupilPosition.x:F6},{data.leftPupilPosition.y:F6}," +
                                  $"{data.rightDiameterValid}," +
                                  $"{data.rightPupilDiameter:F3}," +
                                  $"{data.rightPositionValid}," +
                                  $"{data.rightPupilPosition.x:F6},{data.rightPupilPosition.y:F6}");
                }
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            // ファイルが実際に存在するか確認
            if (File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                int dataCount = hasData ? pupilDataList.Count : 0;
                Debug.Log($"[EyeDataRecorder] ✓ 瞳孔データを保存しました: {filePath} (サイズ: {fileSize} bytes, データ件数: {dataCount}件)");
            }
            else
            {
                Debug.LogError($"[EyeDataRecorder] ✗ ファイルの保存に失敗しました: {filePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeDataRecorder] ✗ 瞳孔データの保存中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    /// <summary>
    /// スペースキーが押されたときにアンカーを記録
    /// </summary>
    void RecordAnchor()
    {
        float anchorTime = Time.time - recordingStartTime;
        int anchorIndex = anchorDataList.Count + 1;

        AnchorData anchor = new AnchorData
        {
            timestamp = anchorTime,
            anchorIndex = anchorIndex
        };

        anchorDataList.Add(anchor);
        Debug.Log($"[EyeDataRecorder] ✓ アンカー #{anchorIndex} を記録しました (タイムスタンプ: {anchorTime:F3}秒)");
    }

    /// <summary>
    /// アンカーデータをCSVファイルに保存
    /// </summary>
    void SaveAnchorDataToCSV()
    {
        // データがなくても空のCSVファイルを出力する
        bool hasData = anchorDataList.Count > 0;
        if (!hasData)
        {
            Debug.LogWarning("[EyeDataRecorder] アンカーデータがありません（空のCSVファイルを出力します）");
        }

        try
        {
            // フォルダが存在しない場合は再作成
            if (!Directory.Exists(dataFolderPath))
            {
                Directory.CreateDirectory(dataFolderPath);
                Debug.Log($"[EyeDataRecorder] データフォルダを再作成しました: {dataFolderPath}");
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{anchorDataFileName}_{timestamp}.csv";
            string filePath = Path.Combine(dataFolderPath, fileName).Replace('/', Path.DirectorySeparatorChar);

            Debug.Log($"[EyeDataRecorder] アンカーデータの保存を開始します... パス: {filePath}");

            StringBuilder csv = new StringBuilder();

            // ヘッダー行
            csv.AppendLine("AnchorIndex,Timestamp");

            // データ行（データがない場合は空行のみ）
            if (hasData)
            {
                foreach (var anchor in anchorDataList)
                {
                    csv.AppendLine($"{anchor.anchorIndex},{anchor.timestamp:F3}");
                }
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            // ファイルが実際に存在するか確認
            if (File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                int dataCount = hasData ? anchorDataList.Count : 0;
                Debug.Log($"[EyeDataRecorder] ✓ アンカーデータを保存しました: {filePath} (サイズ: {fileSize} bytes, データ件数: {dataCount}件)");
            }
            else
            {
                Debug.LogError($"[EyeDataRecorder] ✗ ファイルの保存に失敗しました: {filePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeDataRecorder] ✗ アンカーデータの保存中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    /// <summary>
    /// すべてのデータを統合して1つのCSVファイルに保存
    /// </summary>
    void SaveUnifiedDataToCSV()
    {
        try
        {
            // フォルダが存在しない場合は作成
            if (!Directory.Exists(dataFolderPath))
            {
                Directory.CreateDirectory(dataFolderPath);
                Debug.Log($"[EyeDataRecorder] データフォルダを作成しました: {dataFolderPath}");
            }

            // 共有タイムスタンプを取得
            string timestamp = SharedPositionDataRecorder.GetSharedTimestamp();
            if (string.IsNullOrEmpty(timestamp))
            {
                timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            string fileName = $"UnifiedData_{timestamp}.csv";
            string filePath = Path.Combine(dataFolderPath, fileName).Replace('/', Path.DirectorySeparatorChar);

            Debug.Log($"[EyeDataRecorder] 統合データの保存を開始します... パス: {filePath}");

            StringBuilder csv = new StringBuilder();

            // ヘッダー行（すべてのカラムを含む）
            csv.AppendLine("Timestamp," +
                          "LeftGazeValid,LeftGazePosX,LeftGazePosY,LeftGazePosZ,LeftGazeRotX,LeftGazeRotY,LeftGazeRotZ,LeftGazeRotW," +
                          "RightGazeValid,RightGazePosX,RightGazePosY,RightGazePosZ,RightGazeRotX,RightGazeRotY,RightGazeRotZ,RightGazeRotW," +
                          "LeftDiameterValid,LeftPupilDiameter,LeftPositionValid,LeftPupilPosX,LeftPupilPosY," +
                          "RightDiameterValid,RightPupilDiameter,RightPositionValid,RightPupilPosX,RightPupilPosY," +
                          "AnchorIndex," +
                          "HumanMPositionX,HumanMPositionY,HumanMPositionZ," +
                          "BallPositionX,BallPositionY,BallPositionZ," +
                          "Ball2PositionX,Ball2PositionY,Ball2PositionZ," +
                          "CubeHumanPositionX,CubeHumanPositionY,CubeHumanPositionZ");

            // すべてのタイムスタンプを収集してソート
            var allTimestamps = new SortedSet<float>();
            foreach (var gaze in gazeDataList) allTimestamps.Add(gaze.timestamp);
            foreach (var pupil in pupilDataList) allTimestamps.Add(pupil.timestamp);
            foreach (var anchor in anchorDataList) allTimestamps.Add(anchor.timestamp);

            // PositionDataとCubeHumanPositionDataのタイムスタンプも取得
            var positionData = SharedPositionDataRecorder.GetAllUnifiedData();
            foreach (var pos in positionData) allTimestamps.Add(pos.timestamp);

            // HumanMWalkerからCube-humanデータを取得
            var cubeHumanData = GetCubeHumanData();
            foreach (var cube in cubeHumanData) allTimestamps.Add(cube.timestamp);

            // 各タイムスタンプごとにデータ行を生成（データがない場合はヘッダーのみ）
            if (allTimestamps.Count == 0)
            {
                // データが一切ない場合は、ヘッダーのみのCSVファイルを保存
                Debug.LogWarning("[EyeDataRecorder] すべてのデータタイプが空です（ヘッダーのみのCSVファイルを出力します）");
            }
            else
            {
                foreach (float ts in allTimestamps)
                {
                    // Gazeデータを検索
                    var gaze = gazeDataList.FirstOrDefault(g => Mathf.Abs(g.timestamp - ts) < 0.001f);
                    // Pupilデータを検索
                    var pupil = pupilDataList.FirstOrDefault(p => Mathf.Abs(p.timestamp - ts) < 0.001f);
                    // Anchorデータを検索
                    var anchor = anchorDataList.FirstOrDefault(a => Mathf.Abs(a.timestamp - ts) < 0.001f);
                    // Positionデータを検索
                    var posData = positionData.FirstOrDefault(p => Mathf.Abs(p.timestamp - ts) < 0.001f);
                    // Cube-humanデータを検索
                    var cubeData = cubeHumanData.FirstOrDefault(c => Mathf.Abs(c.timestamp - ts) < 0.001f);

                    // データが存在するかチェック（タイムスタンプが一致する場合）
                    bool hasGaze = Mathf.Abs(gaze.timestamp - ts) < 0.001f;
                    bool hasPupil = Mathf.Abs(pupil.timestamp - ts) < 0.001f;
                    bool hasAnchor = Mathf.Abs(anchor.timestamp - ts) < 0.001f;
                    bool hasPos = Mathf.Abs(posData.timestamp - ts) < 0.001f;
                    bool hasCube = Mathf.Abs(cubeData.timestamp - ts) < 0.001f;

                    // データ行を生成
                    csv.AppendLine($"{ts:F6}," +
                                  $"{(hasGaze ? gaze.leftGazeValid.ToString() : "")}," +
                                  $"{(hasGaze ? gaze.leftGazePosition.x.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.leftGazePosition.y.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.leftGazePosition.z.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.leftGazeRotation.x.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.leftGazeRotation.y.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.leftGazeRotation.z.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.leftGazeRotation.w.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.rightGazeValid.ToString() : "")}," +
                                  $"{(hasGaze ? gaze.rightGazePosition.x.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.rightGazePosition.y.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.rightGazePosition.z.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.rightGazeRotation.x.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.rightGazeRotation.y.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.rightGazeRotation.z.ToString("F6") : "")}," +
                                  $"{(hasGaze ? gaze.rightGazeRotation.w.ToString("F6") : "")}," +
                                  $"{(hasPupil ? pupil.leftDiameterValid.ToString() : "")}," +
                                  $"{(hasPupil ? pupil.leftPupilDiameter.ToString("F3") : "")}," +
                                  $"{(hasPupil ? pupil.leftPositionValid.ToString() : "")}," +
                                  $"{(hasPupil ? pupil.leftPupilPosition.x.ToString("F6") : "")}," +
                                  $"{(hasPupil ? pupil.leftPupilPosition.y.ToString("F6") : "")}," +
                                  $"{(hasPupil ? pupil.rightDiameterValid.ToString() : "")}," +
                                  $"{(hasPupil ? pupil.rightPupilDiameter.ToString("F3") : "")}," +
                                  $"{(hasPupil ? pupil.rightPositionValid.ToString() : "")}," +
                                  $"{(hasPupil ? pupil.rightPupilPosition.x.ToString("F6") : "")}," +
                                  $"{(hasPupil ? pupil.rightPupilPosition.y.ToString("F6") : "")}," +
                                  $"{(hasAnchor ? anchor.anchorIndex.ToString() : "")}," +
                                  $"{(hasPos && posData.humanMPosition.HasValue ? posData.humanMPosition.Value.x.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.humanMPosition.HasValue ? posData.humanMPosition.Value.y.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.humanMPosition.HasValue ? posData.humanMPosition.Value.z.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.ballPosition.HasValue ? posData.ballPosition.Value.x.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.ballPosition.HasValue ? posData.ballPosition.Value.y.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.ballPosition.HasValue ? posData.ballPosition.Value.z.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.ball2Position.HasValue ? posData.ball2Position.Value.x.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.ball2Position.HasValue ? posData.ball2Position.Value.y.ToString("F6") : "")}," +
                                  $"{(hasPos && posData.ball2Position.HasValue ? posData.ball2Position.Value.z.ToString("F6") : "")}," +
                                  $"{(hasCube ? cubeData.positionX.ToString("F6") : "")}," +
                                  $"{(hasCube ? cubeData.positionY.ToString("F6") : "")}," +
                                  $"{(hasCube ? cubeData.positionZ.ToString("F6") : "")}");
                }
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            // ファイルが実際に存在するか確認
            if (File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                Debug.Log($"[EyeDataRecorder] ✓ 統合データを保存しました: {filePath} (サイズ: {fileSize} bytes, データ件数: {allTimestamps.Count}件)");
            }
            else
            {
                Debug.LogError($"[EyeDataRecorder] ✗ 統合データの保存に失敗しました: {filePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[EyeDataRecorder] ✗ 統合データの保存中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
        }
    }

    /// <summary>
    /// Cube-humanデータを取得（HumanMWalkerから）
    /// </summary>
    private List<CubeHumanPositionData> GetCubeHumanData()
    {
        var result = new List<CubeHumanPositionData>();
        var cubeHuman = GameObject.Find("Cube-human");
        if (cubeHuman != null)
        {
            var walker = cubeHuman.GetComponent<HumanMWalker>();
            if (walker != null)
            {
                var humanData = walker.GetCubeHumanPositionData();
                // HumanMWalker.CubeHumanPositionDataからEyeDataRecorder.CubeHumanPositionDataに変換
                foreach (var hd in humanData)
                {
                    result.Add(new CubeHumanPositionData
                    {
                        timestamp = hd.timestamp,
                        positionX = hd.positionX,
                        positionY = hd.positionY,
                        positionZ = hd.positionZ
                    });
                }
            }
        }
        return result;
    }

    // Cube-humanデータ構造（HumanMWalkerと同じ）
    [System.Serializable]
    public struct CubeHumanPositionData
    {
        public float timestamp;
        public float positionX;
        public float positionY;
        public float positionZ;
    }

    void OnGUI()
    {
        // 画面上に現在の状態を表示
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = isRecording ? Color.red : Color.green;

        // 記録中の情報は左上に表示する
        float panelWidth = 360f;
        float panelX = 10f;

        string status = isRecording ? "記録中..." : "待機中";
        GUI.Label(new Rect(panelX, 10, panelWidth, 50), $"[EyeDataRecorder] {status}", style);

        if (RequiresAutoStop())
        {
            float autoStopDuration = GetAutoStopDuration();
            GUI.Label(new Rect(panelX, 60, panelWidth, 30), $"エンターキーで記録開始（{autoStopDuration}秒で自動終了）");
        }
        else
        {
            GUI.Label(new Rect(panelX, 60, panelWidth, 30), "エンターキーで記録開始/停止");
        }

        if (isRecording)
        {
            float recordingDuration = Time.time - recordingStartTime;
            float actualFrequency = gazeDataList.Count > 0 ? gazeDataList.Count / recordingDuration : 0f;
            float elapsedSinceStart = measurementStartTime > 0f ? Time.time - measurementStartTime : recordingDuration;

            GUI.Label(new Rect(panelX, 100, panelWidth, 30), $"視線データ: {gazeDataList.Count}件");
            GUI.Label(new Rect(panelX, 130, panelWidth, 30), $"瞳孔データ: {pupilDataList.Count}件");
            GUI.Label(new Rect(panelX, 160, panelWidth, 30), $"アンカー: {anchorDataList.Count}件");
            GUI.Label(new Rect(panelX, 190, panelWidth, 30), $"実際の周波数: {actualFrequency:F1}Hz / {targetRecordingFrequency}Hz");
            GUI.Label(new Rect(panelX, 220, panelWidth, 30), $"記録時間: {recordingDuration:F2}秒");
            GUI.Label(new Rect(panelX, 250, panelWidth, 30), $"開始からの経過: {elapsedSinceStart:F2}秒");
            GUI.Label(new Rect(panelX, 280, panelWidth, 30), "スペースキーでアンカーを打てます");

            // 自動終了するシーンの場合は残り時間を表示
            if (RequiresAutoStop())
            {
                float autoStopDuration = GetAutoStopDuration();
                float remainingTime = autoStopDuration - recordingDuration;
                if (remainingTime > 0f)
                {
                    GUI.Label(new Rect(panelX, 310, panelWidth, 30), $"残り時間: {remainingTime:F1}秒");
                }
                else
                {
                    GUI.Label(new Rect(panelX, 310, panelWidth, 30), "まもなく終了...");
                }
            }
        }
    }
}
