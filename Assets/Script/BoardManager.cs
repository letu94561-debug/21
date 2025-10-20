using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class BoardManager : MonoBehaviour
{
    public int width = 6;
    public int height = 6;
    public GameObject tilePrefab;// gán prefab `Tile.cs` và ảnh
    public heartmanager heartManager; // Tham chiếu đến heart manager

    // Bật/tắt cách sinh mũi tên (ngẫu nhiên hay xác định)
    public bool useRandomArrows = false;
    public int randomSeed = 0; // hạt giống ngẫu nhiên (nếu bật random)
    public bool ensureSolvableRandom = true; // khi random, tự sửa vòng lặp để đảm bảo có lời giải

    [Header("Tùy chọn độ khó")]
    public bool usePotentialField = true; // Thuật toán tiềm năng giúp khó hơn nhưng vẫn có lời giải
    [Range(1, 12)] public int potentialExitCount = 4; // số lối thoát ở viền
    [Range(0, 5)] public int potentialNoiseJitter = 2; // độ nhiễu (0 = đều, cao = đường đi quanh co)
    [Range(0, 36)] public int blockerCount = 0; // số lượng chướng ngại rải sau khi sinh mũi tên
    [Range(0f, 1f)] public float edgeOutwardProbability = 0.5f; // xác suất ô viền trỏ ra ngoài để người chơi xóa nhanh
    public bool avoidOrthogonalArrowCollisions = true; // hạn chế mũi tên đâm vào nhau theo góc vuông

    [Header("Kiểm tra lời giải và tái sinh")]
    public bool regenerateIfNoMove = true; // nếu không có nước đi hợp lệ, tái random lại
    [Range(1, 50)] public int regenerateAttempts = 8; // số lần thử tái sinh
    private bool isInternalRegen = false; // cờ tránh đệ quy khi tái sinh

    public RectTransform boardContainer; // tùy chọn cho chế độ UI
    public Transform worldParent; // tùy chọn cho chế độ world-space
    public Vector2 cellSize = Vector2.one; // world-space khoảng cách giữa các ô
    public Vector2 origin = Vector2.zero;  // world-space điểm bắt đầu (góc trên-trái)
    public Vector2 spacing = Vector2.zero; // world-space khoảng cách thêm giữa các ô
    Tile[,] tiles;

    public Sprite[] Arrowsprites;
    public Sprite blockerSprite;
    public Sprite emptySprite;

    [Header("Image Pattern Settings")]
    public bool useImagePattern = false;
    public Texture2D patternImage; // Kéo ảnh PNG vào đây
    public float colorThreshold = 0.1f; // Ngưỡng phân biệt màu sắc
    public bool flipImageVertically = true; // Đảo ngược ảnh theo chiều dọc

    [Header("Editor Visualization")]
    public bool showGridInEditor = true;
    public Color gridColor = Color.white;
    public Color selectedGridColor = Color.yellow;
    public bool alignWithCamera = false; // Mặc định sử dụng vị trí Board
    public bool useBoardPosition = true; // Sử dụng vị trí Board làm trung tâm
    public bool autoUpdateOrigin = true; // Tự động cập nhật origin khi Board di chuyển
    private void Start()
    {
        // Cài đặt ban đầu
        lastBoardPosition = transform.position;

        // Tự động tìm heartmanager trên cùng GameObject nếu chưa được gán
        if (heartManager == null)
        {
            heartManager = GetComponent<heartmanager>();
        }
    }

    private Vector3 lastBoardPosition;

    void Update()
    {
        // Chỉ cập nhật origin khi Board thực sự di chuyển
        if (autoUpdateOrigin && useBoardPosition)
        {
            if (Vector3.Distance(transform.position, lastBoardPosition) > 0.01f)
            {
                UpdateOriginFromBoardPosition();
                lastBoardPosition = transform.position;
            }
        }
    }
    public void GenerateEmptyBoard()
    {
        ClearBoard();
        tiles = new Tile[width, height];

        // Khởi tạo RNG nếu cần
        if (useRandomArrows)
        {
            Random.InitState(randomSeed);
        }

        // Nếu sử dụng pattern từ ảnh PNG
        if (useImagePattern)
        {
            GenerateImagePattern();
            return;
        }

        // Nếu sử dụng pattern trái tim theo tỷ lệ
        if (!useImagePattern && patternImage == null)
        {
            GenerateHeartPattern();
            return;
        }

        // Sử dụng vị trí Board làm gốc thay vì origin
        Vector3 boardOrigin = transform.position;
        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        if (usePotentialField)
        {
            GenerateByPotentialField();
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var parent = (Transform)boardContainer;
                    if (parent == null) parent = worldParent;
                    var go = Instantiate(tilePrefab, parent);
                    var tile = go.GetComponent<Tile>();
                    tile.blockerSprite = blockerSprite;
                    tile.emptySprite = emptySprite;
                    tile.arrowSprites = Arrowsprites;
                    var dir = useRandomArrows ? (ArrowDir)Random.Range(0, 4) : DetermineDeterministicDir(x, y);
                    tile.Init(x, y, TileType.Arrow, dir);
                    // Đặt vị trí trong world nếu không dùng RectTransform
                    if (boardContainer == null)
                    {
                        // Tính vị trí dựa trên vị trí Board, căn giữa
                        Vector3 tilePosition = new Vector3(
                            boardOrigin.x - (width * stepX) * 0.5f + x * stepX,
                            boardOrigin.y + (height * stepY) * 0.5f - y * stepY,
                            boardOrigin.z
                        );
                        go.transform.localPosition = tilePosition;
                    }
                    tile.OnTapped = OnTileTapped;
                    tiles[x, y] = tile;
                }
            }
        }

        // Nếu dùng random nhưng cần đảm bảo có lời giải, sửa vòng lặp để mọi đường đi thoát ra biên
        if (!usePotentialField && useRandomArrows && ensureSolvableRandom)
        {
            FixArrowCycles();
        }

        // Làm mượt random: bias cạnh ngoài và tránh va chạm vuông góc
        if (!usePotentialField && useRandomArrows)
        {
            ApplyEdgeOutwardBias(edgeOutwardProbability);
            if (avoidOrthogonalArrowCollisions)
            {
                ResolveOrthogonalArrowCollisions();
            }
        }

        // Kiểm tra có nước đi hợp lệ không; nếu không, thử tái sinh lại
        if (regenerateIfNoMove && !isInternalRegen)
        {
            bool hasMove = HasAnyImmediateMove();
            if (!hasMove)
            {
                Debug.Log("[Board] Không có nước đi hợp lệ -> thử tái sinh");
                TryRegenerateBoard();
                return; // dừng hàm hiện tại vì đã sinh lại
            }
            else
            {
                Debug.Log("[Board] Có ít nhất 1 nước đi hợp lệ");
            }
        }
    }

    void GenerateByPotentialField()
    {
        // 1) Tạo lưới ô và GameObject như bình thường
        Vector3 boardOrigin = transform.position;
        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var parent = (Transform)boardContainer;
                if (parent == null) parent = worldParent;
                var go = Instantiate(tilePrefab, parent);
                var tile = go.GetComponent<Tile>();
                tile.blockerSprite = blockerSprite;
                tile.emptySprite = emptySprite;
                tile.arrowSprites = Arrowsprites;

                // Tạm thời set Arrow, hướng sẽ xác định sau theo trường tiềm năng
                tile.Init(x, y, TileType.Arrow, ArrowDir.Up);

                if (boardContainer == null)
                {
                    Vector3 tilePosition = new Vector3(
                        boardOrigin.x - (width * stepX) * 0.5f + x * stepX,
                        boardOrigin.y + (height * stepY) * 0.5f - y * stepY,
                        boardOrigin.z
                    );
                    go.transform.localPosition = tilePosition;
                }
                tile.OnTapped = OnTileTapped;
                tiles[x, y] = tile;
            }
        }

        // 2) Chọn các lối thoát ở viền (exit cells)
        List<Vector2Int> exits = PickExitsOnBorder(potentialExitCount);

        // 3) Tính trường tiềm năng bằng BFS từ các exit (khoảng cách tới exit gần nhất)
        int[,] dist = ComputeDistanceField(exits);

        // 4) Thiết lập hướng mỗi ô theo gradient giảm dần của dist, thêm nhiễu nhỏ để tăng độ khó
        ApplyGradientDirections(dist, potentialNoiseJitter);

        // 5) Rải blocker nếu có
        ScatterBlockersLocal(blockerCount);
    }

    List<Vector2Int> PickExitsOnBorder(int count)
    {
        List<Vector2Int> border = new List<Vector2Int>();
        for (int x = 0; x < width; x++) { border.Add(new Vector2Int(x, 0)); border.Add(new Vector2Int(x, height - 1)); }
        for (int y = 1; y < height - 1; y++) { border.Add(new Vector2Int(0, y)); border.Add(new Vector2Int(width - 1, y)); }
        if (border.Count == 0) return new List<Vector2Int>();
        if (useRandomArrows) Random.InitState(randomSeed);
        List<Vector2Int> exits = new List<Vector2Int>();
        for (int i = 0; i < count && border.Count > 0; i++)
        {
            int idx = Random.Range(0, border.Count);
            exits.Add(border[idx]);
            border.RemoveAt(idx);
        }
        if (exits.Count == 0) exits.Add(new Vector2Int(width / 2, 0)); // fallback 1 lối thoát
        return exits;
    }

    int[,] ComputeDistanceField(List<Vector2Int> sources)
    {
        int[,] dist = new int[width, height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) dist[x, y] = int.MaxValue;
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        foreach (var s in sources)
        {
            if (!IsInside(s.x, s.y)) continue;
            dist[s.x, s.y] = 0;
            q.Enqueue(s);
        }
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = p.x + dx[k];
                int ny = p.y + dy[k];
                if (!IsInside(nx, ny)) continue;
                int nd = dist[p.x, p.y] + 1;
                if (nd < dist[nx, ny])
                {
                    dist[nx, ny] = nd;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }
        return dist;
    }

    void ApplyGradientDirections(int[,] dist, int jitter)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var t = tiles[x, y];
                if (t == null) continue;
                int best = dist[x, y];
                ArrowDir bestDir = t.arrowDir;
                // duyệt 4 hướng, chọn ô có khoảng cách nhỏ hơn (đi gần exit hơn)
                for (int k = 0; k < 4; k++)
                {
                    ArrowDir d = (ArrowDir)k;
                    var v = DirToVector2(d);
                    int nx = x + v.x, ny = y + v.y;
                    if (!IsInside(nx, ny)) { bestDir = d; best = -1; break; } // nếu ra ngoài là thoát
                    if (dist[nx, ny] < best)
                    {
                        best = dist[nx, ny];
                        bestDir = d;
                    }
                }
                // thêm nhiễu nhẹ để không quá thẳng, nhưng giữ xu thế giảm dist
                if (jitter > 0 && Random.Range(0, 4 + jitter) < jitter)
                {
                    bestDir = (ArrowDir)Random.Range(0, 4);
                }
                t.arrowDir = bestDir;
                t.RefreshVisual();
            }
        }
    }

    void ScatterBlockersLocal(int count)
    {
        int attempts = 0, placed = 0;
        while (placed < count && attempts < 1000)
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            var t = GetTileAt(x, y);
            if (t != null && t.tileType == TileType.Arrow)
            {
                t.tileType = TileType.Blocker;
                t.RefreshVisual();
                placed++;
            }
            attempts++;
        }
    }

    bool HasAnyImmediateMove()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var t = GetTileAt(x, y);
                if (t == null || t.tileType != TileType.Arrow) continue;
                var v = DirToVector2(t.arrowDir);
                int nx = x + v.x, ny = y + v.y;
                var target = GetTileAt(nx, ny);
                if (target == null) return true; // ra biên
                if (target.tileType == TileType.Empty || target.tileType == TileType.Revealed) return true; // xóa ngay
                if (target.tileType == TileType.Blocker) return true; // tương tác được
            }
        }
        return false;
    }

    void TryRegenerateBoard()
    {
        int startSeed = randomSeed;
        // Ghi nhận số mũi tên còn lại để giữ độ tiến trình
        int desiredArrowCount = CountCurrentArrows();
        for (int i = 0; i < regenerateAttempts; i++)
        {
            isInternalRegen = true;
            randomSeed = startSeed + 1 + i + Random.Range(0, 9999);
            Debug.Log($"[Board] Tái sinh lần {i + 1}/{regenerateAttempts}, seed={randomSeed}");
            GenerateEmptyBoard();
            // Sau khi sinh mới, cắt giảm số mũi tên để bằng số còn lại trước đó
            TrimArrowsToCount(desiredArrowCount);
            isInternalRegen = false;
            if (HasAnyImmediateMove())
            {
                Debug.Log("[Board] Tái sinh thành công: đã có nước đi");
                return; // ổn rồi
            }
        }
        // nếu vẫn không có, giữ layout cuối cùng (tránh vòng lặp vô hạn)
        Debug.LogWarning("[Board] Hết số lần tái sinh nhưng vẫn không có nước đi. Giữ layout hiện tại.");
        randomSeed = startSeed;
    }

    int CountCurrentArrows()
    {
        int c = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var t = GetTileAt(x, y);
                if (t != null && t.tileType == TileType.Arrow) c++;
            }
        }
        return c;
    }

    void TrimArrowsToCount(int desired)
    {
        // Đếm số mũi tên hiện tại
        List<Vector2Int> arrows = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var t = GetTileAt(x, y);
                if (t != null && t.tileType == TileType.Arrow) arrows.Add(new Vector2Int(x, y));
            }
        }
        if (arrows.Count <= desired) return; // đã ít hơn hoặc bằng
        // Ngẫu nhiên xóa bớt về đúng số lượng mong muốn
        int toRemove = arrows.Count - desired;
        for (int i = 0; i < toRemove; i++)
        {
            int idx = Random.Range(0, arrows.Count);
            var p = arrows[idx];
            arrows.RemoveAt(idx);
            var t = GetTileAt(p.x, p.y);
            if (t == null) { i--; continue; }
            if (t.tileType != TileType.Arrow) { i--; continue; }
            RevealTile(p.x, p.y);
        }
    }

    // Gọi hàm này sau mỗi thay đổi để đảm bảo luôn có nước đi trong suốt ván chơi
    public void EnsurePlayableNow()
    {
        if (!regenerateIfNoMove || isInternalRegen) return;
        if (!HasAnyImmediateMove())
        {
            Debug.Log("[Board] Không còn nước đi -> tái sinh giữa ván");
            TryRegenerateBoard();
        }
    }
    void OnTileTapped(Tile tile)
    {
        GameManager.Instance.HandleTileTap(tile.x, tile.y);
    }
    public Tile GetTileAt(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return null;

        return tiles[x, y];

    }
    public void SetTile(int x, int y, TileType type, ArrowDir dir)
    {
        var t = GetTileAt(x, y);
        if (t == null) return;
        t.tileType = type;
        t.arrowDir = dir;
        t.RefreshVisual();
    }
    public void RevealTile(int x, int y)
    {
        var t = GetTileAt(x, y);
        if (t == null) return;
        t.tileType = TileType.Revealed;
        t.RefreshVisual();

    }

    public bool TryActivateTile(int x, int y)
    {
        var t = GetTileAt(x, y);
        if (t == null) return false;
        if (t.tileType == TileType.Arrow)
        {
            Vector2Int dir = DirToVector2(t.arrowDir);
            int tx = x + dir.x;
            int ty = y + dir.y;
            var target = GetTileAt(tx, ty);
            if (target == null)
            {
                // Ra ngoài bảng: chuyển ô hiện tại thành đã mở
                RevealTile(x, y);
                return true;
            }
            if (target.tileType == TileType.Blocker)
            {
                // Xóa chướng ngại và mở ô đích
                RevealTile(tx, ty);
                // Đồng thời mở ô nguồn
                RevealTile(x, y);
                return true;
            }
            if (target.tileType == TileType.Arrow)
            {
                // Gặp mũi tên khác: trừ 1 heart và không biến mất
                if (heartManager != null)
                {
                    heartManager.LoseHeart();
                }
                return false;
            }
            // Đích là ô trống/đã mở: mũi tên hiện tại biến mất
            if (target.tileType == TileType.Empty || target.tileType == TileType.Revealed)
            {
                RevealTile(x, y);
                return true;
            }
        }
        else if (t.tileType == TileType.Blocker)
        {
            // tapping blocker does nothing (unless booster used)
            return false;
        }
        return false;


    }
    Vector2Int DirToVector2(ArrowDir d)
    {
        switch (d)
        {
            case ArrowDir.Up: return new Vector2Int(0, -1);
            case ArrowDir.Down: return new Vector2Int(0, 1);
            case ArrowDir.Left: return new Vector2Int(-1, 0);
            case ArrowDir.Right: return new Vector2Int(1, 0);
        }
        return Vector2Int.zero;
    }

    bool IsInside(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    Vector2Int NextFrom(int x, int y)
    {
        var t = GetTileAt(x, y);
        if (t == null || t.tileType != TileType.Arrow) return new Vector2Int(int.MinValue, int.MinValue);
        var d = DirToVector2(t.arrowDir);
        return new Vector2Int(x + d.x, y + d.y);
    }

    void FixArrowCycles()
    {
        // 0 = chưa thăm, 1 = đang thăm, 2 = đã xử lý xong
        int[,] state = new int[width, height];
        List<Vector2Int> stack = new List<Vector2Int>(width * height);

        for (int sy = 0; sy < height; sy++)
        {
            for (int sx = 0; sx < width; sx++)
            {
                if (GetTileAt(sx, sy)?.tileType != TileType.Arrow) continue;
                if (state[sx, sy] != 0) continue;

                int cx = sx, cy = sy;
                stack.Clear();
                while (true)
                {
                    if (!IsInside(cx, cy))
                    {
                        // Đã thoát ra biên; đánh dấu đường đi này là xong
                        foreach (var p in stack)
                        {
                            state[p.x, p.y] = 2;
                        }
                        break;
                    }

                    if (state[cx, cy] == 2)
                    {
                        // Đã được xử lý trước đó
                        foreach (var p in stack)
                        {
                            state[p.x, p.y] = 2;
                        }
                        break;
                    }

                    if (state[cx, cy] == 1)
                    {
                        // Phát hiện vòng lặp. Chọn một ô trong vòng và đổi hướng ra ngoài biên.
                        int idx = stack.FindIndex(v => v.x == cx && v.y == cy);
                        if (idx >= 0)
                        {
                            Vector2Int toFix = stack[stack.Count - 1];
                            RedirectOutward(toFix.x, toFix.y);
                        }
                        // Đánh dấu các ô đã duyệt là xong và dừng chuỗi này
                        foreach (var p in stack)
                        {
                            state[p.x, p.y] = 2;
                        }
                        break;
                    }

                    state[cx, cy] = 1;
                    stack.Add(new Vector2Int(cx, cy));
                    var next = NextFrom(cx, cy);
                    cx = next.x;
                    cy = next.y;
                }
            }
        }

        // Làm mới hiển thị sau khi có thay đổi hướng
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var t = GetTileAt(x, y);
                if (t != null) t.RefreshVisual();
            }
        }
    }

    void RedirectOutward(int x, int y)
    {
        var t = GetTileAt(x, y);
        if (t == null || t.tileType != TileType.Arrow) return;

        // Ưu tiên hướng giúp thoát ra ngay hoặc tiến gần hơn tới biên
        ArrowDir best = t.arrowDir;
        int bestScore = int.MinValue;
        for (int dir = 0; dir < 4; dir++)
        {
            ArrowDir d = (ArrowDir)dir;
            var v = DirToVector2(d);
            int nx = x + v.x;
            int ny = y + v.y;
            int score;
            if (!IsInside(nx, ny))
            {
                // Thoát ngay là tốt nhất
                score = 1000000;
            }
            else
            {
                // Điểm cao hơn nếu tiến gần biên hơn
                int beforeMin = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                int afterMin = Mathf.Min(Mathf.Min(nx, width - 1 - nx), Mathf.Min(ny, height - 1 - ny));
                score = beforeMin - afterMin; // dương nếu gần biên hơn
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }
        t.arrowDir = best;
    }

    void ApplyEdgeOutwardBias(float probability)
    {
        if (probability <= 0f) return;
        for (int x = 0; x < width; x++)
        {
            TrySetOutward(new Vector2Int(x, 0), ArrowDir.Down, probability);
            TrySetOutward(new Vector2Int(x, height - 1), ArrowDir.Up, probability);
        }
        for (int y = 1; y < height - 1; y++)
        {
            TrySetOutward(new Vector2Int(0, y), ArrowDir.Left, probability);
            TrySetOutward(new Vector2Int(width - 1, y), ArrowDir.Right, probability);
        }
    }

    void TrySetOutward(Vector2Int p, ArrowDir outward, float probability)
    {
        var t = GetTileAt(p.x, p.y);
        if (t == null || t.tileType != TileType.Arrow) return;
        if (Random.value < probability)
        {
            t.arrowDir = outward;
            t.RefreshVisual();
        }
    }

    void ResolveOrthogonalArrowCollisions()
    {
        // Với mỗi mũi tên, nếu ngay phía trước là mũi tên theo trục vuông góc, đổi hướng để giảm xung đột
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var t = GetTileAt(x, y);
                if (t == null || t.tileType != TileType.Arrow) continue;
                var v = DirToVector2(t.arrowDir);
                int nx = x + v.x, ny = y + v.y;
                var fwd = GetTileAt(nx, ny);
                if (fwd == null || fwd.tileType != TileType.Arrow) continue;

                bool orthogonal =
                    (t.arrowDir == ArrowDir.Up || t.arrowDir == ArrowDir.Down) && (fwd.arrowDir == ArrowDir.Left || fwd.arrowDir == ArrowDir.Right) ||
                    (t.arrowDir == ArrowDir.Left || t.arrowDir == ArrowDir.Right) && (fwd.arrowDir == ArrowDir.Up || fwd.arrowDir == ArrowDir.Down);

                if (orthogonal)
                {
                    // Chọn hướng thay thế giúp tiến gần biên hơn
                    ArrowDir alt = DetermineDeterministicDir(x, y);
                    t.arrowDir = alt;
                    t.RefreshVisual();
                }
            }
        }
    }

    // Hướng xác định trỏ về biên gần nhất.
    // Tạo trường hướng ra ngoài giúp mở dần từ mép vào, dễ giải hơn.
    ArrowDir DetermineDeterministicDir(int x, int y)
    {
        // Khoảng cách tới các biên (tính theo ô)
        int distLeft = x;
        int distRight = (width - 1) - x;
        int distTop = y;
        int distBottom = (height - 1) - y;

        // Chọn khoảng cách nhỏ nhất. Nếu hòa, ưu tiên ngang để đa dạng.
        int minH = Mathf.Min(distLeft, distRight);
        int minV = Mathf.Min(distTop, distBottom);
        if (minH <= minV)
        {
            return (distLeft <= distRight) ? ArrowDir.Left : ArrowDir.Right;
        }
        else
        {
            return (distTop <= distBottom) ? ArrowDir.Up : ArrowDir.Down;
        }
    }
    public void BomAt(int cx, int cy, int radius)
    {
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                RevealTile(x, y);
            }
        }

    }
    public void ClearBoard()
    {
        Transform parent = boardContainer != null ? (Transform)boardContainer : worldParent;
        if (parent == null) return;
        foreach (Transform t in parent)
            Destroy(t.gameObject);
    }
    public void ClearRow(int row)
    {
        for (int x = 0; x < width; x++)
        {
            RevealTile(x, row);
        }
    }

    // Hiển thị lưới trong Scene view khi không chạy game
    void OnDrawGizmos()
    {
        if (!showGridInEditor) return;

        DrawGrid(gridColor);
    }

    void OnDrawGizmosSelected()
    {
        if (!showGridInEditor) return;

        DrawGrid(selectedGridColor);
    }

    void DrawGrid(Color color)
    {
        Gizmos.color = color;

        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        // Tính vị trí gốc của lưới
        Vector3 gridOrigin;

        if (useBoardPosition)
        {
            // Sử dụng vị trí Board làm trung tâm
            gridOrigin = new Vector3(
                transform.position.x - (width * stepX) * 0.5f,
                transform.position.y + (height * stepY) * 0.5f,
                transform.position.z
            );
        }
        else if (alignWithCamera)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Căn chỉnh lưới với camera
                Vector3 camPos = mainCam.transform.position;
                gridOrigin = new Vector3(
                    camPos.x - (width * stepX) * 0.5f,
                    camPos.y + (height * stepY) * 0.5f,
                    transform.position.z // Giữ nguyên Z của Board
                );
            }
            else
            {
                // Fallback về vị trí Board
                gridOrigin = new Vector3(
                    transform.position.x - (width * stepX) * 0.5f,
                    transform.position.y + (height * stepY) * 0.5f,
                    transform.position.z
                );
            }
        }
        else
        {
            // Sử dụng origin cũ
            gridOrigin = new Vector3(origin.x, origin.y, transform.position.z);
        }

        // Vẽ đường dọc
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = new Vector3(gridOrigin.x + x * stepX, gridOrigin.y, 0);
            Vector3 end = new Vector3(gridOrigin.x + x * stepX, gridOrigin.y - height * stepY, 0);
            Gizmos.DrawLine(start, end);
        }

        // Vẽ đường ngang
        for (int y = 0; y <= height; y++)
        {
            Vector3 start = new Vector3(gridOrigin.x, gridOrigin.y - y * stepY, 0);
            Vector3 end = new Vector3(gridOrigin.x + width * stepX, gridOrigin.y - y * stepY, 0);
            Gizmos.DrawLine(start, end);
        }

        // Vẽ các ô vuông nhỏ để dễ nhìn
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 center = new Vector3(
                    gridOrigin.x + x * stepX + stepX * 0.5f,
                    gridOrigin.y - y * stepY - stepY * 0.5f,
                    0
                );

                // Vẽ hình vuông nhỏ ở giữa mỗi ô
                Vector3 size = new Vector3(stepX * 0.1f, stepY * 0.1f, 0);
                Gizmos.DrawWireCube(center, size);
            }
        }

        // Vẽ điểm trung tâm để dễ nhìn
        Vector3 centerPoint = new Vector3(
            gridOrigin.x + width * stepX * 0.5f,
            gridOrigin.y - height * stepY * 0.5f,
            0
        );
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(centerPoint, 0.2f);
    }

    // Thêm nút trong Inspector để tạo lưới thử nghiệm
    [ContextMenu("Tạo Lưới Thử Nghiệm")]
    void CreateTestGrid()
    {
        if (Application.isPlaying) return;

        // Xóa lưới cũ nếu có
        ClearBoard();

        // Tạo lưới thử nghiệm
        GenerateEmptyBoard();

        Debug.Log($"Đã tạo lưới {width}x{height} để thử nghiệm!");
    }

    [ContextMenu("Xóa Lưới")]
    void ClearTestGrid()
    {
        if (Application.isPlaying) return;

        ClearBoard();
        Debug.Log("Đã xóa lưới thử nghiệm!");
    }

    [ContextMenu("Căn Chỉnh Camera Với Lưới")]
    void AlignCameraWithGrid()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("Không tìm thấy Main Camera!");
            return;
        }

        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        // Tính vị trí trung tâm của lưới
        Vector3 gridCenter = new Vector3(
            origin.x + width * stepX * 0.5f,
            origin.y - height * stepY * 0.5f,
            mainCam.transform.position.z // Giữ nguyên độ sâu Z
        );

        // Di chuyển camera đến trung tâm lưới
        mainCam.transform.position = gridCenter;

        Debug.Log($"Đã căn chỉnh camera với lưới tại vị trí: {gridCenter}");
    }

    [ContextMenu("Căn Chỉnh Lưới Với Camera")]
    void AlignGridWithCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("Không tìm thấy Main Camera!");
            return;
        }

        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        // Căn chỉnh origin với camera
        Vector3 camPos = mainCam.transform.position;
        origin = new Vector2(
            camPos.x - (width * stepX) * 0.5f,
            camPos.y + (height * stepY) * 0.5f
        );

        Debug.Log($"Đã căn chỉnh lưới với camera. Origin mới: {origin}");
    }

    [ContextMenu("Cập Nhật Origin Theo Vị Trí Board")]
    void UpdateOriginFromBoardPosition()
    {
        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        // Cập nhật origin dựa trên vị trí Board
        origin = new Vector2(
            transform.position.x - (width * stepX) * 0.5f,
            transform.position.y + (height * stepY) * 0.5f
        );

        // Chỉ log khi được gọi thủ công, không log trong Update
        if (!Application.isPlaying || !autoUpdateOrigin)
        {
            Debug.Log($"Đã cập nhật origin theo vị trí Board: {origin}");
        }
    }

    [ContextMenu("Di Chuyển Board Đến Origin")]
    void MoveBoardToOrigin()
    {
        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        // Di chuyển Board đến vị trí origin
        Vector3 newPosition = new Vector3(
            origin.x + (width * stepX) * 0.5f,
            origin.y - (height * stepY) * 0.5f,
            transform.position.z
        );

        transform.position = newPosition;
        Debug.Log($"Đã di chuyển Board đến vị trí: {newPosition}");
    }

    [ContextMenu("Căn Chỉnh Lưới Với Board Ngay Lập Tức")]
    void AlignGridWithBoardImmediately()
    {
        // Tắt auto update tạm thời
        bool wasAutoUpdate = autoUpdateOrigin;
        autoUpdateOrigin = false;

        // Cập nhật origin theo vị trí Board hiện tại
        UpdateOriginFromBoardPosition();

        // Bật lại auto update
        autoUpdateOrigin = wasAutoUpdate;

        Debug.Log("Đã căn chỉnh lưới với vị trí Board hiện tại!");
    }

    [ContextMenu("Tạo Lại Lưới Với Vị Trí Đúng")]
    void RegenerateBoardWithCorrectPosition()
    {
        if (Application.isPlaying) return;

        // Xóa lưới cũ
        ClearBoard();

        // Tạo lại lưới với vị trí Board hiện tại
        GenerateEmptyBoard();

        Debug.Log($"Đã tạo lại lưới {width}x{height} tại vị trí Board: {transform.position}");
    }

    // Hàm tạo pattern từ ảnh PNG
    public void GenerateImagePattern()
    {
        Vector3 boardOrigin = transform.position;
        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        // Tạo các tile với pattern từ ảnh
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var parent = (Transform)boardContainer;
                if (parent == null) parent = worldParent;
                var go = Instantiate(tilePrefab, parent);
                var tile = go.GetComponent<Tile>();
                tile.blockerSprite = blockerSprite;
                tile.emptySprite = emptySprite;
                tile.arrowSprites = Arrowsprites;

                // Đọc màu từ ảnh PNG
                int colorRegion = GetColorRegionFromImage(x, y);

                if (colorRegion >= 0)
                {
                    // Có màu trong ảnh: tạo mũi tên random
                    ArrowDir randomDir = (ArrowDir)Random.Range(0, 4);
                    tile.Init(x, y, TileType.Arrow, randomDir);
                }
                else
                {
                    // Không có màu: ô trống
                    tile.Init(x, y, TileType.Empty, ArrowDir.Up);
                }

                // Đặt vị trí trong world
                if (boardContainer == null)
                {
                    Vector3 tilePosition = new Vector3(
                        boardOrigin.x - (width * stepX) * 0.5f + x * stepX,
                        boardOrigin.y + (height * stepY) * 0.5f - y * stepY,
                        boardOrigin.z
                    );
                    go.transform.localPosition = tilePosition;
                }

                tile.OnTapped = OnTileTapped;
                tiles[x, y] = tile;
            }
        }

        // Đảm bảo có ít nhất một nước đi hợp lệ
        EnsurePlayableNow();
    }

    // Đọc vùng màu từ ảnh PNG
    int GetColorRegionFromImage(int x, int y)
    {
        if (patternImage == null)
        {
            Debug.LogWarning("Chưa có ảnh pattern! Hãy kéo ảnh PNG vào trường 'patternImage'");
            return -1;
        }

        // Tính tỷ lệ vị trí trong ảnh
        float u = (float)x / (width - 1);
        float v = (float)y / (height - 1);

        // Đảo ngược Y nếu cần (để khớp với Unity)
        if (flipImageVertically)
        {
            v = 1.0f - v;
        }

        // Lấy màu từ ảnh
        Color pixelColor = patternImage.GetPixelBilinear(u, v);

        // Kiểm tra độ sáng để phân vùng
        float brightness = (pixelColor.r + pixelColor.g + pixelColor.b) / 3f;
        float alpha = pixelColor.a;

        // Nếu trong suốt hoặc quá tối thì không có vùng
        if (alpha < 0.1f || brightness < 0.1f)
        {
            return -1; // Không có vùng
        }

        // Phân vùng dựa trên độ sáng
        if (brightness > 0.8f) return 0; // Sáng = vùng 1
        else if (brightness > 0.5f) return 1; // Trung bình = vùng 2  
        else if (brightness > 0.2f) return 2; // Tối = vùng 3
        else return -1; // Quá tối = không có vùng
    }

    // Hàm match để random mũi tên theo vùng ảnh
    public void MatchArrows()
    {
        if (tiles == null) return;

        Debug.Log("[Board] Đang random mũi tên theo vùng ảnh PNG...");

        int[] regionCounts = new int[3]; // Đếm số mũi tên trong mỗi vùng

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = GetTileAt(x, y);
                if (tile == null || tile.tileType != TileType.Arrow) continue;

                // Xác định vùng màu từ ảnh
                int colorRegion = GetColorRegionFromImage(x, y);
                if (colorRegion >= 0 && colorRegion < regionCounts.Length)
                {
                    regionCounts[colorRegion]++;

                    // Random hướng mũi tên mới
                    ArrowDir newDir = (ArrowDir)Random.Range(0, 4);
                    tile.arrowDir = newDir;
                    tile.RefreshVisual();
                }
            }
        }

        // Hiển thị thống kê vùng màu
        Debug.Log($"[Board] Thống kê vùng ảnh: Vùng 1={regionCounts[0]}, Vùng 2={regionCounts[1]}, Vùng 3={regionCounts[2]}");

        // Đảm bảo vẫn có nước đi hợp lệ sau khi random
        EnsurePlayableNow();

        Debug.Log("[Board] Đã random xong mũi tên theo vùng ảnh PNG!");
    }

    [ContextMenu("Tạo Pattern Từ Ảnh PNG")]
    void CreateImagePattern()
    {
        if (Application.isPlaying) return;

        // Xóa lưới cũ
        ClearBoard();

        // Bật pattern từ ảnh
        useImagePattern = true;

        // Tạo lưới với pattern từ ảnh
        GenerateEmptyBoard();

        Debug.Log("Đã tạo pattern từ ảnh PNG!");
    }

    [ContextMenu("Random Mũi Tên Theo Vùng Ảnh")]
    void RandomArrowsFromImage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Chỉ có thể random mũi tên khi đang chạy game!");
            return;
        }

        MatchArrows();
    }

    [ContextMenu("Tắt Pattern Ảnh")]
    void DisableImagePattern()
    {
        useImagePattern = false;
        Debug.Log("Đã tắt pattern ảnh. Sẽ sử dụng logic tạo mũi tên thông thường.");
    }

    [ContextMenu("Đảo Ngược Ảnh")]
    void FlipImage()
    {
        flipImageVertically = !flipImageVertically;
        Debug.Log($"Đã {(flipImageVertically ? "bật" : "tắt")} đảo ngược ảnh theo chiều dọc");
    }

    [ContextMenu("Test Đọc Ảnh")]
    void TestImageReading()
    {
        if (patternImage == null)
        {
            Debug.LogWarning("Chưa có ảnh pattern!");
            return;
        }

        Debug.Log($"[Board] Đang test đọc ảnh: {patternImage.name} ({patternImage.width}x{patternImage.height})");
        Debug.Log($"[Board] Flip vertically: {flipImageVertically}");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int region = GetColorRegionFromImage(x, y);
                if (region >= 0)
                {
                    Debug.Log($"Ô ({x},{y}) = Vùng {region}");
                }
            }
        }
    }

    // Kiểm tra xem điểm có nằm trong viền trái tim không (theo tỷ lệ thống nhất)
    bool IsInHeartShape(int x, int y)
    {
        // Tính tỷ lệ vị trí so với lưới (0-1)
        float relativeX = (float)x / (width - 1);
        float relativeY = (float)y / (height - 1);

        // Tâm trái tim ở giữa lưới
        float centerX = 0.5f;
        float centerY = 0.4f; // Hơi lệch lên trên một chút

        // Tính khoảng cách từ tâm (chuẩn hóa)
        float dx = (relativeX - centerX) / 0.3f; // 30% chiều rộng
        float dy = (relativeY - centerY) / 0.3f; // 30% chiều cao

        // Công thức trái tim đơn giản (chuẩn hóa)
        float heart = Mathf.Pow(dx * dx + dy * dy - 1, 3) - dx * dx * dy * dy * dy;
        return heart <= 0;
    }

    // Hàm tạo pattern trái tim theo tỷ lệ thống nhất
    public void GenerateHeartPattern()
    {
        Vector3 boardOrigin = transform.position;
        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);

        // Tạo các tile với pattern trái tim
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var parent = (Transform)boardContainer;
                if (parent == null) parent = worldParent;
                var go = Instantiate(tilePrefab, parent);
                var tile = go.GetComponent<Tile>();
                tile.blockerSprite = blockerSprite;
                tile.emptySprite = emptySprite;
                tile.arrowSprites = Arrowsprites;

                // Kiểm tra xem có trong viền trái tim không
                bool isInHeart = IsInHeartShape(x, y);

                if (isInHeart)
                {
                    // Trong viền trái tim: tạo mũi tên random
                    ArrowDir randomDir = (ArrowDir)Random.Range(0, 4);
                    tile.Init(x, y, TileType.Arrow, randomDir);
                }
                else
                {
                    // Ngoài viền: ô trống
                    tile.Init(x, y, TileType.Empty, ArrowDir.Up);
                }

                // Đặt vị trí trong world
                if (boardContainer == null)
                {
                    Vector3 tilePosition = new Vector3(
                        boardOrigin.x - (width * stepX) * 0.5f + x * stepX,
                        boardOrigin.y + (height * stepY) * 0.5f - y * stepY,
                        boardOrigin.z
                    );
                    go.transform.localPosition = tilePosition;
                }

                tile.OnTapped = OnTileTapped;
                tiles[x, y] = tile;
            }
        }

        // Đảm bảo có ít nhất một nước đi hợp lệ
        EnsurePlayableNow();
    }

    // Hàm match cho trái tim: random mũi tên trong viền trái tim
    public void MatchArrowsHeart()
    {
        if (tiles == null) return;

        Debug.Log("[Board] Đang random mũi tên trong viền trái tim...");

        int heartCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = GetTileAt(x, y);
                if (tile == null) continue;

                // Chỉ random mũi tên trong viền trái tim
                if (tile.tileType == TileType.Arrow && IsInHeartShape(x, y))
                {
                    // Random hướng mũi tên mới
                    ArrowDir newDir = (ArrowDir)Random.Range(0, 4);
                    tile.arrowDir = newDir;
                    tile.RefreshVisual();
                    heartCount++;
                }
            }
        }

        Debug.Log($"[Board] Đã random {heartCount} mũi tên trong viền trái tim!");
    }

    [ContextMenu("Tạo Pattern Trái Tim Theo Tỷ Lệ")]
    void CreateHeartPatternProportional()
    {
        if (Application.isPlaying) return;

        // Xóa lưới cũ
        ClearBoard();

        // Bật pattern trái tim
        useImagePattern = false; // Tắt pattern ảnh

        // Tạo lưới với pattern trái tim theo tỷ lệ
        GenerateHeartPattern();

        Debug.Log($"Đã tạo pattern trái tim theo tỷ lệ cho lưới {width}x{height}!");
    }

    [ContextMenu("Test Tỷ Lệ Trái Tim")]
    void TestHeartProportion()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Chỉ có thể test khi đang chạy game!");
            return;
        }

        Debug.Log($"[Board] Test tỷ lệ trái tim cho lưới {width}x{height}:");

        int heartCount = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsInHeartShape(x, y))
                {
                    heartCount++;
                    Debug.Log($"Ô ({x},{y}) trong trái tim");
                }
            }
        }

        float percentage = (float)heartCount / (width * height) * 100f;
        Debug.Log($"[Board] Trái tim chiếm {heartCount}/{width * height} ô ({percentage:F1}%)");
    }
}

