using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GridRow
{
    public int[] columns;
}

[System.Serializable]
public class LevelData
{
    public int width;
    public int height;
    public GridRow[] rows;

    public LevelData(int width, int height, int[][] initialDots)
    {
        this.width = width;
        this.height = height;
        this.rows = new GridRow[height];

        for (int y = 0; y < height; y++)
        {
            rows[y] = new GridRow { columns = new int[width] };
            for (int x = 0; x < width; x++)
            {
                rows[y].columns[x] = initialDots[y][x];
            }
        }
    }

    public int[,] CloneDots()
    {
        int[,] copy = new int[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                copy[y, x] = rows[y].columns[x];
        return copy;
    }
}

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    [SerializeField] private int width = 5;
    [SerializeField] private int height = 5;
    [SerializeField] private float cellSize = 1.2f;
    [SerializeField] private int currentLevel = 1;

    [Header("Prefabs")]
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private GameObject dotPrefab;

    [Header("UI Panels")]
    [SerializeField] private GameObject winModalPanel;

    private Node[,] nodeGrid;
    private List<LevelData> levels = new List<LevelData>();
    private int[,] dynamicLevelData;
    private int totalColorsCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        CreateLevels();
    }

    private void Start()
    {
        if (winModalPanel) winModalPanel.SetActive(false);
        LoadLevel(currentLevel);
    }

    private void CreateLevels()
    {
        levels.Clear();
        // lvl 1
        levels.Add(new LevelData(5, 5, new int[][] {
            new int[] { 1, 0, 2, 0, 3 },
            new int[] { 0, 0, 4, 0, 5 },
            new int[] { 0, 0, 0, 0, 0 },
            new int[] { 0, 2, 0, 3, 0 },
            new int[] { 0, 1, 4, 5, 0 }
        }));
        // lvl 2
        levels.Add(new LevelData(5, 5, new int[][] {
            new int[] { 1, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 0 },
            new int[] { 0, 0, 3, 0, 0 },
            new int[] { 2, 3, 4, 0, 1 },
            new int[] { 4, 0, 0, 0, 2 }
        }));

        // lvl 3
        levels.Add(new LevelData(5, 5, new int[][] {
            new int[] { 0, 1, 2, 3, 0 },
            new int[] { 0, 0, 0, 4, 0 },
            new int[] { 0, 0, 4, 0, 0 },
            new int[] { 1, 0, 0, 5, 0 },
            new int[] { 2, 0, 5, 3, 0 }
        }));

        //  lvl 4
        levels.Add(new LevelData(5, 5, new int[][] {
            new int[] { 0, 0, 0, 1, 2 },
            new int[] { 1, 0, 0, 0, 0 },
            new int[] { 0, 0, 3, 0, 0 },
            new int[] { 0, 0, 0, 4, 0 },
            new int[] { 2, 4, 3, 0, 0 }
        }));

        // lvl 5
        levels.Add(new LevelData(5, 5, new int[][] {
            new int[] { 0, 0, 0, 1, 2 },
            new int[] { 0, 3, 4, 2, 0 },
            new int[] { 0, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 4 },
            new int[] { 0, 0, 1, 0, 3 }
        }));
    }

    private void LoadLevel(int levelIndex)
    {
        if (levels.Count == 0) return;

        currentLevel = Mathf.Clamp(levelIndex, 1, levels.Count);
        LevelData level = levels[currentLevel - 1];

        width = level.width;
        height = level.height;
        dynamicLevelData = level.CloneDots();

        HashSet<int> colors = new HashSet<int>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (dynamicLevelData[y, x] != 0) colors.Add(dynamicLevelData[y, x]);

        totalColorsCount = colors.Count;

        if (winModalPanel) winModalPanel.SetActive(false);
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        ClearCurrentLines();
        ClearGridVisuals();

        nodeGrid = new Node[width, height];
        Vector3 startPos = new Vector3(-(width - 1) * cellSize / 2f, -(height - 1) * cellSize / 2f, 0f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 spawnPos = startPos + new Vector3(x * cellSize, y * cellSize, 0f);
                GameObject nodeObj = Instantiate(nodePrefab, spawnPos, Quaternion.identity, transform);
                nodeObj.name = $"Node_{x}_{y}";

                Node node = nodeObj.GetComponent<Node>();
                if (!node) continue;

                node.x = x;
                node.y = y;
                node.colorType = dynamicLevelData[y, x];
                node.isDot = node.colorType != 0;
                nodeGrid[x, y] = node;

                if (node.isDot) SpawnDot(node, spawnPos);
            }
        }
    }

    private void SpawnDot(Node node, Vector3 position)
    {
        Vector3 dotPos = position + new Vector3(0f, 0f, -0.1f);
        GameObject dot = Instantiate(dotPrefab, dotPos, Quaternion.identity, node.transform);

        var renderer = dot.GetComponent<SpriteRenderer>();
        if (renderer)
        {
            renderer.color = GetColorByType(node.colorType);
            renderer.sortingOrder = 1;
        }
    }

    private void ClearGridVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    public void ClearCurrentLines()
    {
        LineDrawer.ClearAllPaths();
        foreach (var line in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
        {
            if (line.gameObject.name.StartsWith("Line_")) Destroy(line.gameObject);
        }
    }

    public void CheckWinCondition(Dictionary<int, List<Node>> activePaths)
    {
        if (activePaths == null || activePaths.Count == 0) return;

        int completedConnections = 0;
        HashSet<Node> uniqueFilledNodes = new HashSet<Node>();

        foreach (var pair in activePaths)
        {
            List<Node> path = pair.Value;
            if (path == null || path.Count < 2) continue;

            Node start = path[0];
            Node end = path[path.Count - 1];

            if (start.isDot && end.isDot && start != end && start.colorType == end.colorType)
                completedConnections++;

            foreach (Node n in path) uniqueFilledNodes.Add(n);
        }

        bool allConnected = completedConnections == totalColorsCount;
        bool gridFullyFilled = uniqueFilledNodes.Count == (width * height);

        if (allConnected && gridFullyFilled && winModalPanel && !winModalPanel.activeSelf)
        {
            winModalPanel.SetActive(true);
            Debug.Log($"🎉 Level {currentLevel} Complete!");
        }
    }

    public void OnNextLevelButtonPressed()
    {
        if (winModalPanel) winModalPanel.SetActive(false);

        currentLevel++;
        if (currentLevel <= levels.Count)
        {
            LoadLevel(currentLevel);
        }
    }

    public void OnReplayButtonPressed()
    {
        ClearCurrentLines();
        LoadLevel(currentLevel);
    }

    public Node GetNodeAt(int x, int y)
    {
        return (x >= 0 && x < width && y >= 0 && y < height) ? nodeGrid[x, y] : null;
    }

    private Color GetColorByType(int type)
    {
        return type switch
        {
            1 => Color.red,
            2 => Color.green,
            3 => Color.blue,
            4 => Color.yellow,
            5 => new Color(1f, 0.5f, 0f),
            6 => Color.cyan,
            7 => Color.magenta,
            _ => Color.white
        };
    }
}