using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class LevelData
{
    public int width;
    public int height;
    public int[,] dots;

    public LevelData(int width, int height, int[,] dots)
    {
        this.width = width;
        this.height = height;
        this.dots = dots;
    }

    public int[,] CloneDots()
    {
        int[,] copy = new int[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                copy[y, x] = dots[y, x];
            }
        }

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

    private readonly List<Node> allNodes = new List<Node>();
    private readonly List<LevelData> levels = new List<LevelData>();

    private int[,] dynamicLevelData;
    public int TotalDotsCount { get; private set; }

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0)
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateLevels();
    }

    private void Start()
    {
        if (winModalPanel != null)
        {
            winModalPanel.SetActive(false);
        }

        LoadLevel(currentLevel);
    }

    private void CreateLevels()
    {
        levels.Clear();

        levels.Add(new LevelData(
            5, 5,
            new int[,]
            {
                {1,0,0,0,1},
                {0,0,0,0,0},
                {2,0,3,0,2},
                {0,0,0,0,0},
                {3,0,4,0,4}
            }));

        levels.Add(new LevelData(
            5, 5,
            new int[,]
            {
                {1,0,0,2,0},
                {0,0,0,0,0},
                {3,0,4,0,3},
                {0,0,0,0,0},
                {1,0,4,0,2}
            }));

        levels.Add(new LevelData(
            5, 5,
            new int[,]
            {
                {1,0,0,0,2},
                {0,3,0,0,0},
                {0,0,4,0,0},
                {0,0,0,0,0},
                {1,3,0,4,2}
            }));

        levels.Add(new LevelData(
            5, 5,
            new int[,]
            {
                {1,0,2,0,1},
                {0,0,0,0,0},
                {3,0,4,0,3},
                {0,0,0,0,0},
                {2,0,4,0,0}
            }));

        levels.Add(new LevelData(
            5, 5,
            new int[,]
            {
                {1,0,0,0,2},
                {0,0,4,0,0},
                {3,0,0,0,3},
                {0,0,4,0,0},
                {1,0,0,0,2}
            }));
    }

    private void LoadLevel(int levelIndex)
    {
        if (levels.Count == 0)
        {
            Debug.LogError("No levels found.");
            return;
        }

        levelIndex = Mathf.Clamp(levelIndex, 1, levels.Count);
        currentLevel = levelIndex;

        LevelData level = levels[currentLevel - 1];

        if (!IsLevelDataValid(level))
        {
            Debug.LogError($"Level {currentLevel} is invalid.");
            return;
        }

        width = level.width;
        height = level.height;

        // Deep copy to avoid modifying the original level definition
        dynamicLevelData = level.CloneDots();

        // Clear win panel if it's showing
        if (winModalPanel != null && winModalPanel.activeSelf)
        {
            winModalPanel.SetActive(false);
        }

        GenerateGrid();

        // Clear any existing lines
        ClearCurrentLines();
    }

    private bool IsLevelDataValid(LevelData level)
    {
        if (level == null || level.dots == null)
        {
            return false;
        }

        if (level.width <= 0 || level.height <= 0)
        {
            return false;
        }

        if (level.dots.GetLength(0) != level.height || level.dots.GetLength(1) != level.width)
        {
            Debug.LogError(
                $"Level size mismatch. Expected [{level.height},{level.width}] but got [{level.dots.GetLength(0)},{level.dots.GetLength(1)}].");
            return false;
        }

        Dictionary<int, int> colorCounts = new Dictionary<int, int>();

        for (int y = 0; y < level.height; y++)
        {
            for (int x = 0; x < level.width; x++)
            {
                int value = level.dots[y, x];
                if (value == 0)
                {
                    continue;
                }

                if (!colorCounts.ContainsKey(value))
                {
                    colorCounts[value] = 0;
                }

                colorCounts[value]++;
            }
        }

        foreach (var pair in colorCounts)
        {
            if (pair.Value != 2)
            {
                Debug.LogWarning($"Color {pair.Key} appears {pair.Value} times. Flow-style levels usually require exactly 2 dots per color.");
            }
        }

        return true;
    }

    private void GenerateGrid()
    {
        // Clear all current lines and grid visuals FIRST
        ClearCurrentLines();
        ClearGridVisuals();

        allNodes.Clear();
        TotalDotsCount = 0;

        Vector3 startPos = new Vector3(
            -(width - 1) * cellSize / 2f,
            -(height - 1) * cellSize / 2f,
            0f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 spawnPos = startPos + new Vector3(x * cellSize, y * cellSize, 0f);

                GameObject nodeObject = Instantiate(nodePrefab, spawnPos, Quaternion.identity, transform);
                nodeObject.name = $"Node_{x}_{y}";

                Node node = nodeObject.GetComponent<Node>();
                if (node == null)
                {
                    Debug.LogError($"Node prefab is missing Node component: {nodePrefab.name}");
                    continue;
                }

                node.x = x;
                node.y = y;

                int nodeType = dynamicLevelData[y, x];
                node.colorType = nodeType;
                node.isDot = nodeType != 0;

                allNodes.Add(node);

                if (nodeType != 0)
                {
                    TotalDotsCount++;

                    Vector3 dotSpawnPos = spawnPos + new Vector3(0f, 0f, -0.1f);
                    GameObject dot = Instantiate(dotPrefab, dotSpawnPos, Quaternion.identity, nodeObject.transform);

                    SpriteRenderer dotRenderer = dot.GetComponent<SpriteRenderer>();
                    if (dotRenderer != null)
                    {
                        dotRenderer.color = GetColorByType(nodeType);
                        dotRenderer.sortingOrder = 1;
                    }
                }
            }
        }
    }

    private void ClearGridVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    public void ClearCurrentLines()
    {
        // Use LineDrawer's static method to clear all paths
        LineDrawer.ClearAllPaths();

        // Also clear any leftover LineRenderer objects (backup cleanup)
        LineRenderer[] oldLines = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
        foreach (LineRenderer line in oldLines)
        {
            if (line.gameObject.name.StartsWith("Line_"))
            {
                Destroy(line.gameObject);
            }
        }
    }

    public void CheckWinCondition(Dictionary<int, List<Node>> activePaths)
    {
        if (activePaths == null)
        {
            return;
        }

        // If there are no active paths, we're not done yet
        if (activePaths.Count == 0)
        {
            return;
        }

        // Get all color types that have dots in the level
        HashSet<int> requiredColors = new HashSet<int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int value = dynamicLevelData[y, x];
                if (value != 0)
                {
                    requiredColors.Add(value);
                }
            }
        }

        // Check if every required color has a valid path
        HashSet<int> completedColors = new HashSet<int>();
        HashSet<Node> filledNodes = new HashSet<Node>();

        foreach (var pair in activePaths)
        {
            int colorType = pair.Key;
            List<Node> path = pair.Value;

            if (path == null || path.Count < 2)
            {
                continue;
            }

            Node start = path[0];
            Node end = path[path.Count - 1];

            // Check if this is a valid completed path
            if (start != null && end != null &&
                start.isDot && end.isDot &&
                start != end &&
                start.colorType == end.colorType &&
                start.colorType == colorType)
            {
                completedColors.Add(colorType);
            }

            // Add all nodes in this path to the filled set
            foreach (Node node in path)
            {
                if (node != null)
                {
                    filledNodes.Add(node);
                }
            }
        }

        // WIN CONDITION: 
        // 1. All colors are connected (completedColors count matches requiredColors count)
        // 2. All cells are filled with some path
        bool allColorsConnected = completedColors.Count == requiredColors.Count;
        bool allCellsFilled = filledNodes.Count == width * height;

        if (allColorsConnected && allCellsFilled)
        {
            if (winModalPanel != null && !winModalPanel.activeSelf)
            {
                winModalPanel.SetActive(true);
                Debug.Log($"🎉 LEVEL {currentLevel} COMPLETE! All {requiredColors.Count} colors connected!");
            }
        }
    }

    public void OnNextLevelButtonPressed()
    {
        if (winModalPanel != null)
        {
            winModalPanel.SetActive(false);
        }

        currentLevel++;

        if (currentLevel > levels.Count)
        {
            currentLevel = 1;
        }

        LoadLevel(currentLevel);
    }

    public void OnReplayButtonPressed()
    {
        if (winModalPanel != null)
        {
            winModalPanel.SetActive(false);
        }

        ClearCurrentLines();
        LoadLevel(currentLevel);
    }

    private Color GetColorByType(int type)
    {
        switch (type)
        {
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.blue;
            case 4: return Color.yellow;
            case 5: return new Color(1f, 0.5f, 0f);
            case 6: return Color.cyan;
            case 7: return Color.magenta;
            default: return Color.white;
        }
    }

    public Node GetNodeAt(int x, int y)
    {
        foreach (Node node in allNodes)
        {
            if (node.x == x && node.y == y)
            {
                return node;
            }
        }
        return null;
    }

    private void CreateSafeFlowPattern()
    {
        dynamicLevelData = new int[height, width];
        bool[,] visited = new bool[height, width];

        int numColors = width - 1;
        int colorCounter = 1;

        for (int i = 0; i < numColors * 2; i++)
        {
            if (colorCounter > numColors)
            {
                break;
            }

            int startX = Random.Range(0, width);
            int startY = Random.Range(0, height);

            if (visited[startY, startX])
            {
                continue;
            }

            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int current = new Vector2Int(startX, startY);

            path.Add(current);
            visited[current.y, current.x] = true;

            int targetLength = Random.Range(3, 6);

            for (int step = 0; step < targetLength; step++)
            {
                List<Vector2Int> neighbors = GetValidNeighbors(current, visited);
                if (neighbors.Count == 0)
                {
                    break;
                }

                Vector2Int next = neighbors[Random.Range(0, neighbors.Count)];
                path.Add(next);
                visited[next.y, next.x] = true;
                current = next;
            }

            if (path.Count >= 3)
            {
                Vector2Int startPoint = path[0];
                Vector2Int endPoint = path[path.Count - 1];

                dynamicLevelData[startPoint.y, startPoint.x] = colorCounter;
                dynamicLevelData[endPoint.y, endPoint.x] = colorCounter;

                colorCounter++;
            }
            else
            {
                foreach (Vector2Int pos in path)
                {
                    visited[pos.y, pos.x] = false;
                }
            }
        }

        if (colorCounter == 1)
        {
            dynamicLevelData[0, 0] = 1;
            dynamicLevelData[height - 1, width - 1] = 1;
        }
    }

    private List<Vector2Int> GetValidNeighbors(Vector2Int pos, bool[,] visited)
    {
        List<Vector2Int> valid = new List<Vector2Int>();

        foreach (Vector2Int dir in Directions)
        {
            Vector2Int next = pos + dir;

            if (next.x >= 0 && next.x < width && next.y >= 0 && next.y < height)
            {
                if (!visited[next.y, next.x])
                {
                    valid.Add(next);
                }
            }
        }

        return valid;
    }

    private bool HasDeadEnds()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (dynamicLevelData[y, x] != 0)
                {
                    continue;
                }

                int neighborCount = 0;

                foreach (Vector2Int dir in Directions)
                {
                    int nx = x + dir.x;
                    int ny = y + dir.y;

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        neighborCount++;
                    }
                }

                if (neighborCount <= 1)
                {
                    return true;
                }
            }
        }

        return false;
    }
}