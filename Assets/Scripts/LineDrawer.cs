using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class LineDrawer : MonoBehaviour
{
    private LineRenderer currentLineRenderer;
    private List<Node> pathNodes = new List<Node>();
    private int activeColorType = 0;
    private bool isLineFinished = false;

    private static Dictionary<int, LineRenderer> activeLines = new Dictionary<int, LineRenderer>();
    private static Dictionary<int, List<Node>> activePaths = new Dictionary<int, List<Node>>();

    public float lineWidth = 0.4f;

    void Update()
    {
        ProcessInput();
    }

    private void ProcessInput()
    {

#if UNITY_EDITOR || UNITY_STANDALONE
        var button = Mouse.current.leftButton;
        bool started = button.wasPressedThisFrame;
        bool pressed = button.isPressed;
        bool released = button.wasReleasedThisFrame;
#else
        if (Touchscreen.current == null) return;
        var touch = Touchscreen.current.primaryTouch.press;
        bool started = touch.wasPressedThisFrame;
        bool pressed = touch.isPressed;
        bool released = touch.wasReleasedThisFrame;
#endif

        if (started)
            CheckStartNode();
        else if (pressed && currentLineRenderer != null && !isLineFinished)
            CheckContinueNode();
        else if (released)
            FinishLine();
    }

    private void CheckStartNode()
    {
        RaycastHit2D hit = Physics2D.Raycast(GetPointerWorldPosition(), Vector2.zero);
        if (hit.collider == null) return;

        Node startNode = hit.collider.GetComponent<Node>();
        if (startNode != null && startNode.isDot)
        {
            activeColorType = startNode.colorType;
            isLineFinished = false;

            ResetLineColor(activeColorType);

            GameObject lineObj = new GameObject($"Line_{activeColorType}");
            currentLineRenderer = lineObj.AddComponent<LineRenderer>();
            currentLineRenderer.startWidth = lineWidth;
            currentLineRenderer.endWidth = lineWidth;
            currentLineRenderer.positionCount = 1;

            Vector3 startPos = startNode.transform.position;
            startPos.z = -0.2f;
            currentLineRenderer.SetPosition(0, startPos);

            SpriteRenderer dotRenderer = startNode.transform.GetChild(0).GetComponent<SpriteRenderer>();
            Color color = dotRenderer ? dotRenderer.color : Color.white;
            currentLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            currentLineRenderer.startColor = color;
            currentLineRenderer.endColor = color;

            currentLineRenderer.sortingLayerName = "Default";
            currentLineRenderer.sortingOrder = 5;
            currentLineRenderer.numCornerVertices = 5;
            currentLineRenderer.numCapVertices = 5;

            pathNodes.Clear();
            pathNodes.Add(startNode);

            activeLines[activeColorType] = currentLineRenderer;
            activePaths[activeColorType] = new List<Node>(pathNodes);
        }
    }

    private void CheckContinueNode()
    {
        RaycastHit2D hit = Physics2D.Raycast(GetPointerWorldPosition(), Vector2.zero);
        if (hit.collider == null) return;

        Node currentNode = hit.collider.GetComponent<Node>();
        if (currentNode == null) return;

        if (!pathNodes.Contains(currentNode))
        {
            Node lastNode = pathNodes[pathNodes.Count - 1];

            bool isNeighbor = Mathf.Abs(currentNode.x - lastNode.x) + Mathf.Abs(currentNode.y - lastNode.y) == 1;
            if (!isNeighbor) return;

            if (currentNode.isDot && currentNode.colorType != activeColorType) return;

            foreach (var pair in activePaths)
            {
                if (pair.Key != activeColorType && pair.Value.Contains(currentNode))
                {
                    ResetLineColor(pair.Key);
                    break;
                }
            }

            Vector3 snapPos = currentNode.transform.position;
            snapPos.z = -0.2f;

            pathNodes.Add(currentNode);
            currentLineRenderer.positionCount = pathNodes.Count;
            currentLineRenderer.SetPosition(pathNodes.Count - 1, snapPos);

            activeLines[activeColorType] = currentLineRenderer;
            activePaths[activeColorType] = new List<Node>(pathNodes);

            if (currentNode.isDot && currentNode.colorType == activeColorType && currentNode != pathNodes[0])
            {
                isLineFinished = true;
                GridManager.Instance?.CheckWinCondition(activePaths);
            }
        }

        else if (pathNodes.Count > 1 && currentNode == pathNodes[pathNodes.Count - 2])
        {
            pathNodes.RemoveAt(pathNodes.Count - 1);
            currentLineRenderer.positionCount = pathNodes.Count;
            activePaths[activeColorType] = new List<Node>(pathNodes);
        }
    }

    private void FinishLine()
    {
        if (pathNodes.Count >= 2 && currentLineRenderer != null)
        {
            Node start = pathNodes[0];
            Node end = pathNodes[pathNodes.Count - 1];

            if (start && end && start.isDot && end.isDot && start.colorType == end.colorType && start.colorType == activeColorType)
            {
                currentLineRenderer = null;
                isLineFinished = false;
                GridManager.Instance?.CheckWinCondition(activePaths);
                return;
            }
        }

        if (activeColorType != 0 && !isLineFinished)
        {
            ResetLineColor(activeColorType);
        }

        currentLineRenderer = null;
        pathNodes.Clear();
        isLineFinished = false;
    }

    private void ResetLineColor(int colorType)
    {
        if (activeLines.TryGetValue(colorType, out LineRenderer line) && line != null)
        {
            Destroy(line.gameObject);
            activeLines.Remove(colorType);
        }

        activePaths.Remove(colorType);

        if (activeColorType == colorType)
        {
            currentLineRenderer = null;
            pathNodes.Clear();
            isLineFinished = false;
        }
    }

    public static void ClearAllPaths()
    {
        foreach (var line in activeLines.Values)
        {
            if (line != null) Destroy(line.gameObject);
        }
        activeLines.Clear();
        activePaths.Clear();
    }

    private Vector3 GetPointerWorldPosition()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 pos = Mouse.current.position.ReadValue();
#else
        Vector2 pos = Touchscreen.current.primaryTouch.position.ReadValue();
#endif
        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(pos.x, pos.y, -Camera.main.transform.position.z));
        world.z = 0;
        return world;
    }
}