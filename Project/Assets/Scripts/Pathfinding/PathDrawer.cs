using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class PathDrawer : MonoBehaviour
{
    public List<Cell> path;
    public float drawSpeed = 20.0f;
    public float lineWidth = 0.5f;
    public float heightOffset = 0.25f;
    public float fadeOutDuration = 1.0f; // Durata della dissolvenza
    public float fadeOutDelay = 5.0f;    // Ritardo prima della dissolvenza
    private LineRenderer lineRenderer;
    private Coroutine drawingCoroutine;
    private Material lineMaterial;
    public bool autoFadeOut = true;

    public void DrawPath(List<Cell> path)
    {
        this.path = path;

        if (path == null || path.Count == 0)
            return;

        if (drawingCoroutine != null)
        {
            StopCoroutine(drawingCoroutine);
        }
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        SetupLineRenderer();
        drawingCoroutine = StartCoroutine(AnimatePathSequence());
    }

    private Vector3 GetElevatedPosition(Vector3 position)
    {
        return new Vector3(position.x, position.y + heightOffset, position.z);
    }

    private void SetupLineRenderer()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.green;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.sortingLayerName = "Foreground";
        lineRenderer.sortingOrder = 100;
        Vector3 startPos = GetElevatedPosition(path[0].GetWorldPosition());
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, startPos);
    }

    private IEnumerator AnimatePathSequence()
    {
        // Prima disegna il percorso
        yield return StartCoroutine(AnimatePathDrawing());

        // Se autoFadeOut è true, attendi e poi dissolvi
        if (autoFadeOut)
        {
            yield return new WaitForSeconds(fadeOutDelay);
            yield return StartCoroutine(FadeOutPath());
        }
    }

    private IEnumerator AnimatePathDrawing()
    {
        if (path == null || path.Count < 2)
            yield break;

        lineRenderer.positionCount = path.Count;
        try
        {
            lineRenderer.SetPosition(0, GetElevatedPosition(path[0].GetWorldPosition()));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Attenzionare il path drawer: " + e.Message);
        }

        float segmentDuration = 1f / drawSpeed;

        for (int i = 0; i < path.Count - 1; i++)
        {
            if (lineRenderer == null) yield break; // Esci se il LineRenderer è stato distrutto

            Vector3 startPos = GetElevatedPosition(path[i].GetWorldPosition());
            Vector3 endPos = GetElevatedPosition(path[i + 1].GetWorldPosition());
            float elapsedTime = 0f;

            if (i > 0)
            {
                lineRenderer.positionCount = i + 2;
                lineRenderer.SetPosition(i, startPos);
                lineRenderer.SetPosition(i + 1, startPos);
            }

            while (elapsedTime < segmentDuration)
            {
                if (lineRenderer == null) yield break;
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / segmentDuration;
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                try
                {
                    if (i + 1 < lineRenderer.positionCount)
                    {
                        lineRenderer.SetPosition(i + 1, currentPos);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Attenzionare il path drawer: " + e.Message);
                }
                yield return null; yield return null;
            }
            if (lineRenderer == null) yield break;
            try
            {
                if (i + 1 < lineRenderer.positionCount)
                {
                    lineRenderer.SetPosition(i + 1, endPos);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Attenzionare il path drawer: " + e.Message);
            }
        }
    }


    private IEnumerator FadeOutPath()
    {
        if (lineRenderer == null)
        {
            yield break;
        }
        float elapsedTime = 0f;
        Color startColorBegin = lineRenderer.startColor;
        Color startColorEnd = new Color(startColorBegin.r, startColorBegin.g, startColorBegin.b, 0);
        Color endColorBegin = lineRenderer.endColor;
        Color endColorEnd = new Color(endColorBegin.r, endColorBegin.g, endColorBegin.b, 0);

        while (elapsedTime < fadeOutDuration)
        {
            if (lineRenderer == null)
            {
                yield break;
            }
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeOutDuration;

            lineRenderer.startColor = Color.Lerp(startColorBegin, startColorEnd, t);
            lineRenderer.endColor = Color.Lerp(endColorBegin, endColorEnd, t);

            yield return null;
        }
    }

    public void ClearPath()
    {
        if (drawingCoroutine != null)
        {
            StopCoroutine(drawingCoroutine);
            drawingCoroutine = null;
        }
        StopAllCoroutines();

        if (lineRenderer != null)
        {
            Destroy(lineRenderer);
            lineRenderer = null;
        }

        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
            lineMaterial = null;
        }

        path = null;
    }

    private void OnDrawGizmos()
    {
        if (Camera.current == Camera.main) return;

        if (path == null || path.Count == 0)
            return;

        Gizmos.color = Color.magenta;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 start = GetElevatedPosition(path[i].GetWorldPosition());
            Vector3 end = GetElevatedPosition(path[i + 1].GetWorldPosition());
            Gizmos.DrawLine(start, end);
        }

        Gizmos.color = Color.blue;
        foreach (var cell in path)
        {
            Gizmos.DrawSphere(GetElevatedPosition(cell.GetWorldPosition()), 0.1f);
        }
    }
}