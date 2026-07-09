using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class LineDrawer : MonoBehaviour
{
    private LineRenderer currentLineRenderer;
    private List<Node> pathNodes = new List<Node>();
    private int activeColorType = 0;

    private static Dictionary<int, LineRenderer> activeLines = new Dictionary<int, LineRenderer>();
    private static Dictionary<int, List<Node>> activePaths = new Dictionary<int, List<Node>>();

    public float lineWidth = 0.4f;

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouse();
#else
        HandleTouch();
#endif
    }

    void HandleMouse()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
            CheckStartNode();
        else if (Mouse.current.leftButton.isPressed && currentLineRenderer != null)
            CheckContinueNode();
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
            FinishLine();
    }

    void HandleTouch()
    {
        if (Touchscreen.current == null) return;
        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
            CheckStartNode();
        else if (touch.press.isPressed && currentLineRenderer != null)
            CheckContinueNode();
        else if (touch.press.wasReleasedThisFrame)
            FinishLine();
    }

    void FinishLine()
    {
        // Valid completed path? Keep it.
        if (pathNodes.Count >= 2 && currentLineRenderer != null)
        {
            Node start = pathNodes[0];
            Node end = pathNodes[pathNodes.Count - 1];

            if (start != null && end != null &&
                start.isDot && end.isDot &&
                start.colorType == end.colorType &&
                start.colorType == activeColorType)
            {
                // Valid – line stays, just stop drawing
                currentLineRenderer = null;
                GridManager.Instance?.CheckWinCondition(activePaths);
                return;
            }
        }

        // Invalid or incomplete – destroy it
        if (activeColorType != 0)
            ResetLineColor(activeColorType);

        currentLineRenderer = null;
        pathNodes.Clear();
    }

    Vector3 GetPointerWorldPosition()
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

    void CheckStartNode()
    {
        RaycastHit2D hit = Physics2D.Raycast(GetPointerWorldPosition(), Vector2.zero);
        if (hit.collider != null)
        {
            Node startNode = hit.collider.GetComponent<Node>();
            if (startNode != null && startNode.isDot)
            {
                // Prevent starting a new line for an already‑completed color
                if (activePaths.ContainsKey(startNode.colorType))
                {
                    List<Node> existingPath = activePaths[startNode.colorType];
                    if (existingPath != null && existingPath.Count >= 2)
                    {
                        Node existingStart = existingPath[0];
                        Node existingEnd = existingPath[existingPath.Count - 1];
                        if (existingStart != null && existingEnd != null &&
                            existingStart.isDot && existingEnd.isDot &&
                            existingStart.colorType == existingEnd.colorType)
                            return;
                    }
                }

                activeColorType = startNode.colorType;
                ResetLineColor(activeColorType); // remove any incomplete line of same color

                GameObject lineObj = new GameObject($"Line_{activeColorType}");
                currentLineRenderer = lineObj.AddComponent<LineRenderer>();
                currentLineRenderer.startWidth = lineWidth;
                currentLineRenderer.endWidth = lineWidth;
                currentLineRenderer.positionCount = 1;

                Vector3 startPos = startNode.transform.position;
                startPos.z = -0.2f;
                currentLineRenderer.SetPosition(0, startPos);

                SpriteRenderer dotRenderer = startNode.transform.GetChild(0).GetComponent<SpriteRenderer>();
                Color c = dotRenderer != null ? dotRenderer.color : Color.white;
                currentLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                currentLineRenderer.startColor = c;
                currentLineRenderer.endColor = c;

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
    }

    void CheckContinueNode()
    {
        RaycastHit2D hit = Physics2D.Raycast(GetPointerWorldPosition(), Vector2.zero);
        if (hit.collider != null)
        {
            Node currentNode = hit.collider.GetComponent<Node>();
            if (currentNode != null && !pathNodes.Contains(currentNode))
            {
                Node lastNode = pathNodes[pathNodes.Count - 1];

                if (Mathf.Abs(currentNode.x - lastNode.x) + Mathf.Abs(currentNode.y - lastNode.y) == 1)
                {
                    if (currentNode.isDot && currentNode.colorType != activeColorType)
                        return;

                    // Remove any conflicting path that uses this node
                    foreach (var pair in activePaths)
                    {
                        if (pair.Key != activeColorType && pair.Value.Contains(currentNode))
                        {
                            ResetLineColor(pair.Key);
                            break;
                        }
                    }

                    bool reachedEndDot = currentNode.isDot && currentNode.colorType == activeColorType && currentNode != pathNodes[0];

                    Vector3 snapPos = currentNode.transform.position;
                    snapPos.z = -0.2f;

                    pathNodes.Add(currentNode);
                    currentLineRenderer.positionCount = pathNodes.Count;
                    currentLineRenderer.SetPosition(pathNodes.Count - 1, snapPos);

                    activeLines[activeColorType] = currentLineRenderer;
                    activePaths[activeColorType] = new List<Node>(pathNodes);

                    if (reachedEndDot)
                    {
                        // Valid connection – keep the line, just stop drawing
                        GridManager.Instance?.CheckWinCondition(activePaths);
                        // ✅ DO NOT set currentLineRenderer = null here — that causes the line to be deleted on mouse up!
                        // The line stays referenced, so FinishLine will see it as valid.
                    }
                }
            }
            // Backtracking
            else if (currentNode != null && pathNodes.Count > 1 && currentNode == pathNodes[pathNodes.Count - 2])
            {
                pathNodes.RemoveAt(pathNodes.Count - 1);
                currentLineRenderer.positionCount = pathNodes.Count;
                activePaths[activeColorType] = new List<Node>(pathNodes);
            }
        }
    }

    void ResetLineColor(int colorType)
    {
        if (activeLines.ContainsKey(colorType))
        {
            if (activeLines[colorType] != null)
                Destroy(activeLines[colorType].gameObject);
            activeLines.Remove(colorType);
        }
        if (activePaths.ContainsKey(colorType))
            activePaths.Remove(colorType);

        if (activeColorType == colorType)
        {
            currentLineRenderer = null;
            pathNodes.Clear();
        }
    }

    public static void ClearAllPaths()
    {
        foreach (var line in activeLines.Values)
        {
            if (line != null)
                Destroy(line.gameObject);
        }
        activeLines.Clear();
        activePaths.Clear();
    }
}