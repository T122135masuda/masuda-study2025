using System.Collections.Generic;
using UnityEngine;

public class CourtManager : MonoBehaviour
{
    public static CourtManager Instance { get; private set; }

    [Header("Court / Floor")]
    [Tooltip("バスケコートに相当する floor の MeshRenderer。自動検出されます。")]
    public MeshRenderer floorRenderer;

    [Header("Agents (自動) ")]
    public List<BasketballAgentController> agents = new List<BasketballAgentController>();
    
    [Header("Walls (自動検出)")]
    public List<Collider> wallColliders = new List<Collider>();

    private Bounds _floorBoundsWorld;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (floorRenderer == null)
        {
            floorRenderer = FindFloorRenderer();
        }

        if (floorRenderer != null)
        {
            _floorBoundsWorld = floorRenderer.bounds;
        }
        else
        {
            Debug.LogWarning("CourtManager: floor の MeshRenderer が見つかりませんでした。境界維持が無効になります。");
        }

        RefreshWalls();
    }

    public void RegisterAgent(BasketballAgentController agent)
    {
        if (!agents.Contains(agent))
        {
            agents.Add(agent);
        }
    }

    public void UnregisterAgent(BasketballAgentController agent)
    {
        if (agents.Contains(agent))
        {
            agents.Remove(agent);
        }
    }

    public bool TryGetFloorBounds(out Bounds bounds)
    {
        if (floorRenderer == null)
        {
            bounds = default;
            return false;
        }
        bounds = _floorBoundsWorld;
        return true;
    }

    public void RefreshWalls()
    {
        wallColliders.Clear();

        // 名前指定: wall1, wall2, wall3
        string[] wallNames = { "wall1", "wall2", "wall3" };
        foreach (var name in wallNames)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var cols = go.GetComponentsInChildren<Collider>();
                foreach (var c in cols)
                {
                    if (!wallColliders.Contains(c)) wallColliders.Add(c);
                }
            }
        }

        // タグ検索は削除（Wallタグが定義されていないため）
        // 必要に応じて手動でwallCollidersに追加してください
    }

    private MeshRenderer FindFloorRenderer()
    {
        // 優先: シーン上の "Cube_house" (や類似) の子にある "floor"
        foreach (var go in FindObjectsOfType<Transform>())
        {
            if (go.name.ToLower().Contains("cube_house"))
            {
                var floor = go.Find("floor");
                if (floor != null)
                {
                    var mr = floor.GetComponentInChildren<MeshRenderer>();
                    if (mr != null) return mr;
                }
            }
        }

        // 次: 名前が floor のオブジェクト
        var floorByName = GameObject.Find("floor");
        if (floorByName != null)
        {
            var mr = floorByName.GetComponentInChildren<MeshRenderer>();
            if (mr != null) return mr;
        }

        // タグ検索は削除（Floorタグが定義されていない可能性があるため）
        // 必要に応じて手動でfloorRendererにアサインしてください

        // 最後: 最も大きい水平メッシュを床と推測
        MeshRenderer best = null;
        float bestArea = 0f;
        foreach (var mr in FindObjectsOfType<MeshRenderer>())
        {
            var b = mr.bounds;
            // 水平方向(XZ)の面積が大きいものを候補に
            float area = b.size.x * b.size.z;
            if (area > bestArea)
            {
                bestArea = area;
                best = mr;
            }
        }
        return best;
    }
}


