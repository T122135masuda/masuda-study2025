using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Linq;

/// <summary>
/// HumanMとBallの位置データを統合して記録する共有クラス
/// </summary>
public static class SharedPositionDataRecorder
{
    [System.Serializable]
    public struct UnifiedPositionData
    {
        public float timestamp;
        public Vector3? humanMPosition;
        public Vector3? ballPosition;
    }

    private static List<UnifiedPositionData> _unifiedDataList = new List<UnifiedPositionData>();
    private static object _dataLock = new object();
    private static string _sharedTimestamp = null;
    private static object _timestampLock = new object();
    private static string _teamSuffix = "White";
    private static int _targetFrequency = 120; // 目標記録周波数（Hz）

    /// <summary>
    /// 共有タイムスタンプを設定
    /// </summary>
    public static void SetSharedTimestamp(string timestamp)
    {
        lock (_timestampLock)
        {
            if (_sharedTimestamp == null)
            {
                _sharedTimestamp = timestamp;
            }
        }
    }

    /// <summary>
    /// 共有タイムスタンプを取得
    /// </summary>
    public static string GetSharedTimestamp()
    {
        lock (_timestampLock)
        {
            return _sharedTimestamp ?? DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }

    /// <summary>
    /// チームサフィックスを設定
    /// </summary>
    public static void SetTeamSuffix(string suffix)
    {
        _teamSuffix = suffix;
    }

    /// <summary>
    /// 目標記録周波数を設定
    /// </summary>
    public static void SetTargetFrequency(int frequency)
    {
        _targetFrequency = frequency;
    }

    /// <summary>
    /// HumanMの位置データを追加
    /// </summary>
    public static void AddHumanMPosition(float timestamp, Vector3 position)
    {
        lock (_dataLock)
        {
            // 同じタイムスタンプのデータを探す
            int index = _unifiedDataList.FindIndex(d => Mathf.Abs(d.timestamp - timestamp) < 0.0001f);
            if (index >= 0)
            {
                // 既存のデータを更新
                var existing = _unifiedDataList[index];
                existing.humanMPosition = position;
                _unifiedDataList[index] = existing;
            }
            else
            {
                // 新しいデータを追加
                _unifiedDataList.Add(new UnifiedPositionData
                {
                    timestamp = timestamp,
                    humanMPosition = position,
                    ballPosition = null
                });
            }
        }
    }

    /// <summary>
    /// Ballの位置データを追加
    /// </summary>
    public static void AddBallPosition(float timestamp, Vector3 position)
    {
        lock (_dataLock)
        {
            // 同じタイムスタンプのデータを探す
            int index = _unifiedDataList.FindIndex(d => Mathf.Abs(d.timestamp - timestamp) < 0.0001f);
            if (index >= 0)
            {
                // 既存のデータを更新
                var existing = _unifiedDataList[index];
                existing.ballPosition = position;
                _unifiedDataList[index] = existing;
            }
            else
            {
                // 新しいデータを追加
                _unifiedDataList.Add(new UnifiedPositionData
                {
                    timestamp = timestamp,
                    humanMPosition = null,
                    ballPosition = position
                });
            }
        }
    }

    /// <summary>
    /// データをクリア
    /// </summary>
    public static void Clear()
    {
        lock (_dataLock)
        {
            _unifiedDataList.Clear();
        }
        lock (_timestampLock)
        {
            _sharedTimestamp = null;
        }
    }

    /// <summary>
    /// 統合データをCSVファイルに保存
    /// </summary>
    public static void SaveToCSV(string dataFolderPath, bool enableDebugLogs = false)
    {
        lock (_dataLock)
        {
            if (_unifiedDataList.Count == 0)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning("[SharedPositionDataRecorder] 保存するデータがありません");
                }
                return;
            }

            try
            {
                // フォルダが存在しない場合は作成
                if (!Directory.Exists(dataFolderPath))
                {
                    Directory.CreateDirectory(dataFolderPath);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SharedPositionDataRecorder] データフォルダを作成しました: {dataFolderPath}");
                    }
                }

                string timestamp = GetSharedTimestamp();
                string fileName = $"PositionData_{_teamSuffix}_{timestamp}.csv";
                string filePath = Path.Combine(dataFolderPath, fileName).Replace('/', Path.DirectorySeparatorChar);

                if (enableDebugLogs)
                {
                    Debug.Log($"[SharedPositionDataRecorder] 統合座標データの保存を開始します... パス: {filePath}");
                }

                // タイムスタンプでソート
                var sortedData = _unifiedDataList.OrderBy(d => d.timestamp).ToList();

                // データを90Hzで補間
                var interpolatedData = InterpolateData(sortedData, 90);

                if (enableDebugLogs)
                {
                    Debug.Log($"[SharedPositionDataRecorder] 元のデータ件数: {sortedData.Count}件 → 補間後: {interpolatedData.Count}件");
                    if (sortedData.Count > 0 && interpolatedData.Count > 0)
                    {
                        float originalDuration = sortedData[sortedData.Count - 1].timestamp - sortedData[0].timestamp;
                        float typicalInterval = CalculateTypicalInterval(sortedData);
                        Debug.Log($"[SharedPositionDataRecorder] 記録時間: {originalDuration:F2}秒, 典型的な間隔: {typicalInterval:F6}秒, 補間倍率: {(float)interpolatedData.Count / sortedData.Count:F2}倍");
                    }
                }

                StringBuilder csv = new StringBuilder();

                // ヘッダー行
                csv.AppendLine("Timestamp,HumanMPositionX,HumanMPositionY,HumanMPositionZ,BallPositionX,BallPositionY,BallPositionZ");

                // データ行
                foreach (var data in interpolatedData)
                {
                    string humanMX = data.humanMPosition.HasValue ? data.humanMPosition.Value.x.ToString("F6") : "";
                    string humanMY = data.humanMPosition.HasValue ? data.humanMPosition.Value.y.ToString("F6") : "";
                    string humanMZ = data.humanMPosition.HasValue ? data.humanMPosition.Value.z.ToString("F6") : "";
                    string ballX = data.ballPosition.HasValue ? data.ballPosition.Value.x.ToString("F6") : "";
                    string ballY = data.ballPosition.HasValue ? data.ballPosition.Value.y.ToString("F6") : "";
                    string ballZ = data.ballPosition.HasValue ? data.ballPosition.Value.z.ToString("F6") : "";

                    csv.AppendLine($"{data.timestamp:F6},{humanMX},{humanMY},{humanMZ},{ballX},{ballY},{ballZ}");
                }

                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

                // ファイルが実際に存在するか確認
                if (File.Exists(filePath))
                {
                    long fileSize = new FileInfo(filePath).Length;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SharedPositionDataRecorder] ✓ 統合座標データを保存しました: {filePath} (サイズ: {fileSize} bytes, データ件数: {interpolatedData.Count}件)");
                    }
                }
                else
                {
                    Debug.LogError($"[SharedPositionDataRecorder] ✗ ファイルの保存に失敗しました: {filePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SharedPositionDataRecorder] ✗ 座標データの保存中にエラーが発生しました: {e.Message}\nスタックトレース: {e.StackTrace}");
            }
        }
    }

    /// <summary>
    /// 典型的な間隔を計算
    /// </summary>
    private static float CalculateTypicalInterval(List<UnifiedPositionData> data)
    {
        if (data.Count < 2) return 0.05f; // デフォルト値

        List<float> intervals = new List<float>();
        for (int i = 1; i < data.Count; i++)
        {
            float interval = data[i].timestamp - data[i - 1].timestamp;
            if (interval > 0.0001f)
            {
                intervals.Add(interval);
            }
        }

        if (intervals.Count == 0) return 0.05f;

        intervals.Sort();
        float medianInterval = intervals[intervals.Count / 2];
        float avgInterval = intervals.Sum() / intervals.Count;
        return Mathf.Min(medianInterval, avgInterval);
    }

    /// <summary>
    /// データを線形補間して、120Hz（約0.00833秒間隔）で補完
    /// </summary>
    private static List<UnifiedPositionData> InterpolateData(List<UnifiedPositionData> originalData, int targetFrequency)
    {
        if (originalData.Count == 0)
        {
            return new List<UnifiedPositionData>();
        }

        if (originalData.Count == 1)
        {
            return originalData;
        }

        // 120Hzの間隔を計算（1秒 / 120 = 約0.00833秒）
        float targetInterval = 1.0f / targetFrequency; // 目標周波数の間隔

        // 最初と最後のタイムスタンプを取得
        float startTime = originalData[0].timestamp;
        float endTime = originalData[originalData.Count - 1].timestamp;
        float totalDuration = endTime - startTime;

        List<UnifiedPositionData> interpolated = new List<UnifiedPositionData>();

        // 120Hzの間隔で最初から最後まで一貫してデータを生成
        float currentTime = startTime;
        int originalIndex = 0;

        while (currentTime <= endTime + 0.0001f)
        {
            // 現在のタイムスタンプに最も近い元のデータポイントを探す
            while (originalIndex < originalData.Count - 1 && originalData[originalIndex + 1].timestamp < currentTime)
            {
                originalIndex++;
            }

            UnifiedPositionData interpolatedPoint;

            // ちょうど元のデータポイントと一致する場合
            if (originalIndex < originalData.Count && Mathf.Abs(originalData[originalIndex].timestamp - currentTime) < 0.0001f)
            {
                interpolatedPoint = originalData[originalIndex];
            }
            // 元のデータポイントの間にある場合、補間する
            else if (originalIndex < originalData.Count - 1)
            {
                var current = originalData[originalIndex];
                var next = originalData[originalIndex + 1];
                float timeDiff = next.timestamp - current.timestamp;

                if (timeDiff > 0.0001f)
                {
                    float t = (currentTime - current.timestamp) / timeDiff;
                    t = Mathf.Clamp01(t);

                    interpolatedPoint = new UnifiedPositionData
                    {
                        timestamp = currentTime,
                        humanMPosition = InterpolateVector3(current.humanMPosition, next.humanMPosition, t),
                        ballPosition = InterpolateVector3(current.ballPosition, next.ballPosition, t)
                    };
                }
                else
                {
                    interpolatedPoint = current;
                }
            }
            // 最後のデータポイントを超えている場合
            else
            {
                interpolatedPoint = originalData[originalData.Count - 1];
                interpolatedPoint.timestamp = currentTime;
            }

            interpolated.Add(interpolatedPoint);

            // 次のタイムスタンプに進む
            currentTime += targetInterval;
        }

        return interpolated;
    }

    /// <summary>
    /// Vector3を線形補間（null値の処理を含む）
    /// </summary>
    private static Vector3? InterpolateVector3(Vector3? start, Vector3? end, float t)
    {
        if (!start.HasValue && !end.HasValue)
        {
            return null;
        }

        if (!start.HasValue)
        {
            return end.Value;
        }

        if (!end.HasValue)
        {
            return start.Value;
        }

        return Vector3.Lerp(start.Value, end.Value, t);
    }
}

