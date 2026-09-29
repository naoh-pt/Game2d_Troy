using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates a private runtime copy of a sprite texture and generates filled 2D
/// collision paths from its alpha mask. The source texture is never modified.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class DestructibleTerrain2D : MonoBehaviour
{
    private const string ColliderContainerName = "RuntimeTerrainColliders";

    [Header("References")]
    [SerializeField] private SpriteRenderer terrainRenderer;

    [Header("Alpha Collision")]
    [SerializeField, Range(0.01f, 1f)] private float alphaThreshold = 0.5f;
    [SerializeField, Range(1, 16)] private int colliderSamplingResolution = 4;
    [SerializeField, Min(0f)] private float colliderSimplificationTolerance = 0.05f;

    private Sprite sourceSprite;
    private Sprite runtimeSprite;
    private Texture2D runtimeTexture;
    private Color32[] pixels;
    private PolygonCollider2D terrainCollider;
    private Transform colliderContainer;
    private RectInt spritePixelRect;
    private Vector2 spritePivot;
    private float pixelsPerUnit;
    private bool initialized;
    private bool validationErrorLogged;

    public bool IsInitialized => initialized;

    private void Awake()
    {
        InitializeRuntimeTerrain();
    }

    private void OnValidate()
    {
        alphaThreshold = Mathf.Clamp(alphaThreshold, 0.01f, 1f);
        colliderSamplingResolution = Mathf.Clamp(colliderSamplingResolution, 1, 16);
        colliderSimplificationTolerance = Mathf.Max(0f, colliderSimplificationTolerance);
    }

    private void OnDestroy()
    {
        if (terrainRenderer != null && runtimeSprite != null && terrainRenderer.sprite == runtimeSprite)
            terrainRenderer.sprite = sourceSprite;

        DestroyRuntimeObject(runtimeSprite);
        DestroyRuntimeObject(runtimeTexture);
        if (colliderContainer != null)
            DestroyRuntimeObject(colliderContainer.gameObject);
    }

    /// <summary>Assigns the renderer without changing any tuning values.</summary>
    public void Configure(SpriteRenderer rendererReference)
    {
        terrainRenderer = rendererReference;
    }

    /// <summary>
    /// Removes all terrain pixels inside a world-space circle and rebuilds the
    /// alpha-derived collider only when collision-relevant pixels changed.
    /// </summary>
    public void DestroyCircle(Vector2 worldPosition, float worldRadius)
    {
        if (!initialized || runtimeTexture == null || pixels == null || worldRadius <= 0f)
            return;

        Vector3 localCenter3 = transform.InverseTransformPoint(worldPosition);
        Vector2 localCenter = new Vector2(localCenter3.x, localCenter3.y);
        Vector2 centerPixel = LocalToSpritePixel(localCenter);

        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
        Vector3 localWorldX = worldToLocal.MultiplyVector(new Vector3(worldRadius, 0f, 0f));
        Vector3 localWorldY = worldToLocal.MultiplyVector(new Vector3(0f, worldRadius, 0f));
        float pixelRadiusX = Mathf.Sqrt(localWorldX.x * localWorldX.x + localWorldY.x * localWorldY.x) * pixelsPerUnit;
        float pixelRadiusY = Mathf.Sqrt(localWorldX.y * localWorldX.y + localWorldY.y * localWorldY.y) * pixelsPerUnit;

        int minimumX = Mathf.Max(spritePixelRect.xMin, spritePixelRect.xMin + Mathf.FloorToInt(centerPixel.x - pixelRadiusX));
        int maximumX = Mathf.Min(spritePixelRect.xMax - 1, spritePixelRect.xMin + Mathf.CeilToInt(centerPixel.x + pixelRadiusX));
        int minimumY = Mathf.Max(spritePixelRect.yMin, spritePixelRect.yMin + Mathf.FloorToInt(centerPixel.y - pixelRadiusY));
        int maximumY = Mathf.Min(spritePixelRect.yMax - 1, spritePixelRect.yMin + Mathf.CeilToInt(centerPixel.y + pixelRadiusY));
        if (minimumX > maximumX || minimumY > maximumY)
            return;

        float radiusSquared = worldRadius * worldRadius;
        byte collisionAlpha = AlphaThresholdByte;
        bool visualChanged = false;
        bool collisionChanged = false;
        int textureWidth = runtimeTexture.width;

        for (int y = minimumY; y <= maximumY; y++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                Vector2 spritePixel = new Vector2(
                    x - spritePixelRect.xMin + 0.5f,
                    y - spritePixelRect.yMin + 0.5f);
                Vector3 pixelWorld = transform.TransformPoint(SpritePixelToLocal(spritePixel));
                Vector2 difference = (Vector2)pixelWorld - worldPosition;
                if (difference.sqrMagnitude > radiusSquared)
                    continue;

                int pixelIndex = y * textureWidth + x;
                byte oldAlpha = pixels[pixelIndex].a;
                if (oldAlpha == 0)
                    continue;

                visualChanged = true;
                collisionChanged |= oldAlpha >= collisionAlpha;
                Color32 color = pixels[pixelIndex];
                color.a = 0;
                pixels[pixelIndex] = color;
            }
        }

        if (!visualChanged)
            return;

        UploadPixelRegion(minimumX, minimumY, maximumX, maximumY);
        if (collisionChanged)
            RebuildCollider();
    }

    private void InitializeRuntimeTerrain()
    {
        if (initialized)
            return;

        terrainRenderer = terrainRenderer != null ? terrainRenderer : GetComponent<SpriteRenderer>();
        if (!ValidateConfiguration())
            return;

        sourceSprite = terrainRenderer.sprite;
        Texture2D sourceTexture = sourceSprite.texture;

        Color32[] sourcePixels;
        try
        {
            sourcePixels = sourceTexture.GetPixels32();
        }
        catch (UnityException exception)
        {
            LogValidationError("Terrain texture is not readable. Run the Level 01 destructible terrain setup command. " + exception.Message);
            return;
        }

        Rect sourceRect = sourceSprite.rect;
        spritePixelRect = new RectInt(
            Mathf.RoundToInt(sourceRect.x),
            Mathf.RoundToInt(sourceRect.y),
            Mathf.RoundToInt(sourceRect.width),
            Mathf.RoundToInt(sourceRect.height));
        spritePivot = sourceSprite.pivot;
        pixelsPerUnit = sourceSprite.pixelsPerUnit;

        // Sprite rects retain their logical source dimensions when an importer scales
        // the GPU texture. Reconstruct those logical pixels so rect, pivot, and PPU
        // all remain identical to the source sprite.
        int runtimeWidth = Mathf.Max(spritePixelRect.xMax, spritePixelRect.width);
        int runtimeHeight = Mathf.Max(spritePixelRect.yMax, spritePixelRect.height);
        pixels = ResampleSpritePixels(sourceTexture, sourcePixels, runtimeWidth, runtimeHeight);
        runtimeTexture = new Texture2D(runtimeWidth, runtimeHeight, TextureFormat.RGBA32, false)
        {
            name = sourceTexture.name + "_RuntimeMutable",
            filterMode = sourceTexture.filterMode,
            wrapMode = sourceTexture.wrapMode,
            anisoLevel = sourceTexture.anisoLevel,
            hideFlags = HideFlags.DontSave
        };
        runtimeTexture.SetPixels32(pixels);
        runtimeTexture.Apply(false, false);

        Vector2 normalizedPivot = new Vector2(
            spritePivot.x / spritePixelRect.width,
            spritePivot.y / spritePixelRect.height);
        runtimeSprite = Sprite.Create(
            runtimeTexture,
            sourceRect,
            normalizedPivot,
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            sourceSprite.border,
            false);
        runtimeSprite.name = sourceSprite.name + "_RuntimeMutable";
        runtimeSprite.hideFlags = HideFlags.DontSave;

        // Replacing only the sprite preserves material, color, sorting, and all renderer settings.
        terrainRenderer.sprite = runtimeSprite;
        CreateColliderContainer();
        initialized = true;
        RebuildCollider();
    }

    private bool ValidateConfiguration()
    {
        if (terrainRenderer == null || terrainRenderer.sprite == null)
        {
            LogValidationError("DestructibleTerrain2D requires a SpriteRenderer with a sprite.");
            return false;
        }

        Sprite sprite = terrainRenderer.sprite;
        Rect rect = sprite.rect;
        if (rect.x < 0f || rect.y < 0f || rect.width < 1f || rect.height < 1f)
        {
            LogValidationError("The terrain sprite has an invalid sprite rect.");
            return false;
        }

        if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None)
        {
            LogValidationError("Rotated sprite-atlas packing is unsupported for destructible terrain.");
            return false;
        }

        if (terrainRenderer.drawMode != SpriteDrawMode.Simple)
        {
            LogValidationError("Destructible terrain currently requires SpriteRenderer Draw Mode = Simple.");
            return false;
        }

        if (sprite.pixelsPerUnit <= 0f || rect.width < 1f || rect.height < 1f)
        {
            LogValidationError("The terrain sprite has invalid dimensions or Pixels Per Unit.");
            return false;
        }

        return true;
    }

    private Color32[] ResampleSpritePixels(Texture2D sourceTexture, Color32[] sourcePixels, int runtimeWidth, int runtimeHeight)
    {
        var result = new Color32[runtimeWidth * runtimeHeight];
        Rect importedTextureRect = sourceSprite.textureRect;

        for (int localY = 0; localY < spritePixelRect.height; localY++)
        {
            float sourceYPosition = Mathf.Lerp(
                importedTextureRect.yMin,
                importedTextureRect.yMax,
                (localY + 0.5f) / spritePixelRect.height);
            int sourceY = Mathf.Clamp(Mathf.FloorToInt(sourceYPosition), 0, sourceTexture.height - 1);
            int destinationRow = (spritePixelRect.yMin + localY) * runtimeWidth + spritePixelRect.xMin;
            int sourceRow = sourceY * sourceTexture.width;
            for (int localX = 0; localX < spritePixelRect.width; localX++)
            {
                float sourceXPosition = Mathf.Lerp(
                    importedTextureRect.xMin,
                    importedTextureRect.xMax,
                    (localX + 0.5f) / spritePixelRect.width);
                int sourceX = Mathf.Clamp(Mathf.FloorToInt(sourceXPosition), 0, sourceTexture.width - 1);
                result[destinationRow + localX] = sourcePixels[sourceRow + sourceX];
            }
        }

        return result;
    }

    private void CreateColliderContainer()
    {
        var containerObject = new GameObject(ColliderContainerName)
        {
            layer = gameObject.layer,
            hideFlags = HideFlags.DontSave
        };
        colliderContainer = containerObject.transform;
        colliderContainer.SetParent(transform, false);
        colliderContainer.localPosition = Vector3.zero;
        colliderContainer.localRotation = Quaternion.identity;
        colliderContainer.localScale = Vector3.one;
        terrainCollider = containerObject.AddComponent<PolygonCollider2D>();
        terrainCollider.isTrigger = false;
        terrainCollider.useDelaunayMesh = false;
    }

    private void UploadPixelRegion(int minimumX, int minimumY, int maximumX, int maximumY)
    {
        int width = maximumX - minimumX + 1;
        int height = maximumY - minimumY + 1;
        var regionPixels = new Color32[width * height];
        int textureWidth = runtimeTexture.width;
        for (int row = 0; row < height; row++)
            Array.Copy(pixels, (minimumY + row) * textureWidth + minimumX, regionPixels, row * width, width);

        runtimeTexture.SetPixels32(minimumX, minimumY, width, height, regionPixels);
        runtimeTexture.Apply(false, false);
    }

    private void RebuildCollider()
    {
        if (terrainCollider == null)
            return;

        int step = Mathf.Max(1, colliderSamplingResolution);
        int gridWidth = Mathf.CeilToInt((float)spritePixelRect.width / step);
        int gridHeight = Mathf.CeilToInt((float)spritePixelRect.height / step);
        bool[] solidCells = BuildSolidCellGrid(gridWidth, gridHeight, step);
        List<BoundaryEdge> edges = BuildBoundaryEdges(solidCells, gridWidth, gridHeight);
        List<List<GridPoint>> gridContours = TraceContours(edges);
        var colliderPaths = new List<List<Vector2>>(gridContours.Count);

        for (int i = 0; i < gridContours.Count; i++)
        {
            List<GridPoint> gridContour = gridContours[i];
            var localContour = new List<Vector2>(gridContour.Count);
            for (int pointIndex = 0; pointIndex < gridContour.Count; pointIndex++)
            {
                GridPoint gridPoint = gridContour[pointIndex];
                Vector2 spritePixel = new Vector2(
                    Mathf.Min(gridPoint.X * step, spritePixelRect.width),
                    Mathf.Min(gridPoint.Y * step, spritePixelRect.height));
                localContour.Add(SpritePixelToLocal(spritePixel));
            }

            List<Vector2> simplified = SimplifyClosedPath(localContour, colliderSimplificationTolerance);
            if (terrainRenderer.flipX ^ terrainRenderer.flipY)
                simplified.Reverse();
            if (simplified.Count >= 3)
                colliderPaths.Add(simplified);
        }

        terrainCollider.enabled = false;
        terrainCollider.pathCount = colliderPaths.Count;
        for (int i = 0; i < colliderPaths.Count; i++)
            terrainCollider.SetPath(i, colliderPaths[i]);
        terrainCollider.enabled = colliderPaths.Count > 0;
    }

    private bool[] BuildSolidCellGrid(int gridWidth, int gridHeight, int step)
    {
        var solid = new bool[gridWidth * gridHeight];
        byte threshold = AlphaThresholdByte;
        int textureWidth = runtimeTexture.width;

        for (int cellY = 0; cellY < gridHeight; cellY++)
        {
            int localMinimumY = cellY * step;
            int localMaximumY = Mathf.Min(localMinimumY + step, spritePixelRect.height);
            for (int cellX = 0; cellX < gridWidth; cellX++)
            {
                int localMinimumX = cellX * step;
                int localMaximumX = Mathf.Min(localMinimumX + step, spritePixelRect.width);
                bool isSolid = false;
                for (int localY = localMinimumY; localY < localMaximumY && !isSolid; localY++)
                {
                    int rowStart = (spritePixelRect.yMin + localY) * textureWidth + spritePixelRect.xMin;
                    for (int localX = localMinimumX; localX < localMaximumX; localX++)
                    {
                        if (pixels[rowStart + localX].a < threshold)
                            continue;
                        isSolid = true;
                        break;
                    }
                }

                solid[cellY * gridWidth + cellX] = isSolid;
            }
        }

        return solid;
    }

    private static List<BoundaryEdge> BuildBoundaryEdges(bool[] solid, int width, int height)
    {
        var edges = new List<BoundaryEdge>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!solid[y * width + x])
                    continue;

                if (y == 0 || !solid[(y - 1) * width + x])
                    edges.Add(new BoundaryEdge(new GridPoint(x, y), new GridPoint(x + 1, y)));
                if (x == width - 1 || !solid[y * width + x + 1])
                    edges.Add(new BoundaryEdge(new GridPoint(x + 1, y), new GridPoint(x + 1, y + 1)));
                if (y == height - 1 || !solid[(y + 1) * width + x])
                    edges.Add(new BoundaryEdge(new GridPoint(x + 1, y + 1), new GridPoint(x, y + 1)));
                if (x == 0 || !solid[y * width + x - 1])
                    edges.Add(new BoundaryEdge(new GridPoint(x, y + 1), new GridPoint(x, y)));
            }
        }

        return edges;
    }

    private static List<List<GridPoint>> TraceContours(List<BoundaryEdge> edges)
    {
        var outgoing = new Dictionary<long, List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            long key = edges[i].Start.Key;
            if (!outgoing.TryGetValue(key, out List<int> indices))
            {
                indices = new List<int>(2);
                outgoing.Add(key, indices);
            }
            indices.Add(i);
        }

        var used = new bool[edges.Count];
        var contours = new List<List<GridPoint>>();
        for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            if (used[edgeIndex])
                continue;

            BoundaryEdge firstEdge = edges[edgeIndex];
            var contour = new List<GridPoint> { firstEdge.Start };
            int currentIndex = edgeIndex;
            bool closed = false;
            for (int guard = 0; guard <= edges.Count; guard++)
            {
                if (used[currentIndex])
                    break;

                used[currentIndex] = true;
                BoundaryEdge current = edges[currentIndex];
                if (current.End.Equals(firstEdge.Start))
                {
                    closed = true;
                    break;
                }

                contour.Add(current.End);
                if (!outgoing.TryGetValue(current.End.Key, out List<int> candidates))
                    break;
                currentIndex = ChooseNextEdge(current, candidates, edges, used);
                if (currentIndex < 0)
                    break;
            }

            if (closed && contour.Count >= 3)
                contours.Add(contour);
        }

        return contours;
    }

    private static int ChooseNextEdge(BoundaryEdge incoming, List<int> candidates, List<BoundaryEdge> edges, bool[] used)
    {
        int incomingX = incoming.End.X - incoming.Start.X;
        int incomingY = incoming.End.Y - incoming.Start.Y;
        int bestIndex = -1;
        int bestScore = int.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidateIndex = candidates[i];
            if (used[candidateIndex])
                continue;

            BoundaryEdge candidate = edges[candidateIndex];
            int outgoingX = candidate.End.X - candidate.Start.X;
            int outgoingY = candidate.End.Y - candidate.Start.Y;
            int cross = incomingX * outgoingY - incomingY * outgoingX;
            int dot = incomingX * outgoingX + incomingY * outgoingY;
            int score = cross > 0 ? 3 : dot > 0 ? 2 : cross < 0 ? 1 : 0;
            if (score <= bestScore)
                continue;
            bestScore = score;
            bestIndex = candidateIndex;
        }

        return bestIndex;
    }

    private static List<Vector2> SimplifyClosedPath(List<Vector2> points, float tolerance)
    {
        if (points.Count <= 3 || tolerance <= 0f)
            return points;

        int splitIndex = 1;
        float greatestDistance = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            float distance = (points[i] - points[0]).sqrMagnitude;
            if (distance <= greatestDistance)
                continue;
            greatestDistance = distance;
            splitIndex = i;
        }

        var firstHalf = new List<Vector2>(splitIndex + 1);
        for (int i = 0; i <= splitIndex; i++)
            firstHalf.Add(points[i]);
        var secondHalf = new List<Vector2>(points.Count - splitIndex + 1);
        for (int i = splitIndex; i < points.Count; i++)
            secondHalf.Add(points[i]);
        secondHalf.Add(points[0]);

        List<Vector2> simplifiedFirst = SimplifyOpenPath(firstHalf, tolerance);
        List<Vector2> simplifiedSecond = SimplifyOpenPath(secondHalf, tolerance);
        var result = new List<Vector2>(simplifiedFirst.Count + simplifiedSecond.Count - 2);
        result.AddRange(simplifiedFirst);
        for (int i = 1; i < simplifiedSecond.Count - 1; i++)
            result.Add(simplifiedSecond[i]);
        return result.Count >= 3 ? result : points;
    }

    private static List<Vector2> SimplifyOpenPath(List<Vector2> points, float tolerance)
    {
        var keep = new bool[points.Count];
        keep[0] = true;
        keep[points.Count - 1] = true;
        SimplifySection(points, 0, points.Count - 1, tolerance * tolerance, keep);

        var result = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
                result.Add(points[i]);
        }
        return result;
    }

    private static void SimplifySection(List<Vector2> points, int first, int last, float toleranceSquared, bool[] keep)
    {
        if (last <= first + 1)
            return;

        Vector2 start = points[first];
        Vector2 end = points[last];
        float greatestDistance = 0f;
        int greatestIndex = -1;
        for (int i = first + 1; i < last; i++)
        {
            float distance = DistanceToSegmentSquared(points[i], start, end);
            if (distance <= greatestDistance)
                continue;
            greatestDistance = distance;
            greatestIndex = i;
        }

        if (greatestIndex < 0 || greatestDistance <= toleranceSquared)
            return;
        keep[greatestIndex] = true;
        SimplifySection(points, first, greatestIndex, toleranceSquared, keep);
        SimplifySection(points, greatestIndex, last, toleranceSquared, keep);
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon)
            return (point - start).sqrMagnitude;
        float interpolation = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return (point - (start + segment * interpolation)).sqrMagnitude;
    }

    private Vector2 LocalToSpritePixel(Vector2 localPosition)
    {
        float x = terrainRenderer.flipX ? -localPosition.x : localPosition.x;
        float y = terrainRenderer.flipY ? -localPosition.y : localPosition.y;
        return new Vector2(x * pixelsPerUnit + spritePivot.x, y * pixelsPerUnit + spritePivot.y);
    }

    private Vector2 SpritePixelToLocal(Vector2 spritePixel)
    {
        float x = (spritePixel.x - spritePivot.x) / pixelsPerUnit;
        float y = (spritePixel.y - spritePivot.y) / pixelsPerUnit;
        if (terrainRenderer.flipX)
            x = -x;
        if (terrainRenderer.flipY)
            y = -y;
        return new Vector2(x, y);
    }

    private byte AlphaThresholdByte => (byte)Mathf.Clamp(Mathf.RoundToInt(alphaThreshold * 255f), 0, 255);

    private void LogValidationError(string message)
    {
        if (validationErrorLogged)
            return;
        validationErrorLogged = true;
        Debug.LogError("[DestructibleTerrain2D] " + message, this);
    }

    private static void DestroyRuntimeObject(UnityEngine.Object runtimeObject)
    {
        if (runtimeObject == null)
            return;
        if (Application.isPlaying)
            Destroy(runtimeObject);
        else
            DestroyImmediate(runtimeObject);
    }

    private readonly struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X;
        public readonly int Y;
        public long Key => ((long)X << 32) ^ (uint)Y;

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
    }

    private readonly struct BoundaryEdge
    {
        public readonly GridPoint Start;
        public readonly GridPoint End;

        public BoundaryEdge(GridPoint start, GridPoint end)
        {
            Start = start;
            End = end;
        }
    }
}
