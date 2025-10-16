using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public int width = 6;
    public int height = 6;
    public GameObject tilePrefab;// gan prefab tile.cs va anh
    
    public RectTransform boardContainer; // optional for UI mode
    public Transform worldParent; // optional for world-space mode
        public Vector2 cellSize = Vector2.one; // world-space khoảng cách giữa các ô
        public Vector2 origin = Vector2.zero;  // world-space điểm bắt đầu (góc trên-trái)
        public Vector2 spacing = Vector2.zero; // world-space khoảng cách thêm giữa các ô
    Tile[,] tiles;

    public Sprite[] Arrowsprites;
    public Sprite blockerSprite;
    public Sprite emptySprite;
    private void Start()
    {
        // cai dat tu dong tao nen
    }
    public void GenerateEmptyBoard()
    {
        ClearBoard();
        tiles = new Tile[width, height];
        for (int y = 0;y < height; y++)
        {
            for (int x = 0;x < width; x++)
                {
                    var parent = (Transform)boardContainer;
                    if (parent == null) parent = worldParent;
                    var go = Instantiate(tilePrefab, parent);
                var tile = go.GetComponent<Tile>();
                    tile.blockerSprite = blockerSprite;
                    tile.emptySprite = emptySprite;
                    tile.arrowSprites = Arrowsprites;
                    tile.Init(x, y,TileType.Arrow, (ArrowDir)Random.Range(0,4));
                    // position in world if not using RectTransform
                    if (boardContainer == null)
                    {
                        float stepX = Mathf.Max(0.0001f, cellSize.x + spacing.x);
                        float stepY = Mathf.Max(0.0001f, cellSize.y + spacing.y);
                        go.transform.localPosition = new Vector3(origin.x + x * stepX, origin.y - y * stepY, 0f);
                    }
                tile.OnTapped = OnTileTapped;
                    tiles[x, y] = tile;
            }
        }
    }
    void OnTileTapped(Tile tile)
    {
        GameManager.Instance.HandleTileTap(tile.x, tile.y);
    }
    public Tile GetTileAt(int x , int y)
    {
        if(x< 0|| x >= width || y <  0 || y >= height) return null;
        
            return tiles[x,y];
        
    }
    public void SetTile(int x, int y, TileType type, ArrowDir dir)
    {
        var t = GetTileAt(x, y);
        if (t == null) return;
        t.tileType = type;
        t.arrowDir = dir;
        t.RefreshVisual();
    }
    public void RevealTile(int x , int y)
    {
        var t = GetTileAt(x, y);
        if (t == null) return;
        t.tileType = TileType.Revealed;
        t.RefreshVisual();

    }

    public bool TryActivateTile(int x , int y)
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
                // off-board: convert this tile into revealed
                RevealTile(x, y);
                return true;
            }
            if (target.tileType == TileType.Blocker)
            {
                // clear blocker and reveal target
                RevealTile(tx, ty);
                // also reveal source
                RevealTile(x, y);
                return true;
            }
            if (target.tileType == TileType.Arrow)
            {
                // attempt to push: if next cell after target is empty or revealed, move target into it
                int nx = tx + dir.x;
                int ny = ty + dir.y;
                var next = GetTileAt(nx, ny);
                if (next != null && (next.tileType == TileType.Revealed || next.tileType == TileType.Empty))
                {
                    // move target arrow into next
                    next.tileType = TileType.Arrow;
                    next.arrowDir = target.arrowDir;
                    next.RefreshVisual();


                    // source becomes revealed
                    RevealTile(x, y);


                    // target becomes revealed (or empty)
                    RevealTile(tx, ty);
                    return true;
                }
                else
                {
                    // cannot move: do nothing, keep directions unchanged
                    return false;
                }
            }
            if (target.tileType == TileType.Revealed || target.tileType == TileType.Empty)
            {
                // move source into target
                target.tileType = TileType.Arrow;
                target.arrowDir = t.arrowDir;
                target.RefreshVisual();
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
            case ArrowDir.Up: return new Vector2Int(0,1) ;
            case ArrowDir.Down: return new Vector2Int(0,-1);
            case ArrowDir.Left: return new Vector2Int(-1,0);
            case ArrowDir.Right: return new Vector2Int(1, 0);
        }
        return Vector2Int.zero;
    }
    public void BomAt(int cx , int cy,int radius) 
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
}
