namespace HDSC
{
    /// <summary>
    /// A single PNG tile with its position in the grid.
    /// </summary>
    public class PngTile
    {
        public int tileX;
        public int tileY;
        public int width;
        public int height;
        public byte[] pngBytes;
    }

    /// <summary>
    /// A grid of PNG tiles forming a larger image.
    /// </summary>
    public class PngTiles
    {
        private readonly PngTile[,] _tiles;
        public int FinalWidth;
        public int FinalHeight;

        /// <summary>
        /// Create a grid of tiles.
        /// </summary>
        /// <param name="cols"> number of columns </param>
        /// <param name="rows"> number of rows </param>
        /// <param name="width"> the final image width </param>
        /// <param name="height"> the final image height </param>
        public PngTiles(int cols, int rows, int width, int height)
        {
            FinalWidth = width;
            FinalHeight = height;
            _tiles = new PngTile[cols, rows];
        }

        /// <summary>
        /// Set a tile at (x,y) with its PNG bytes.
        /// </summary>
        /// <param name="x"> the tile X in the grid [0..Cols-1] </param>
        /// <param name="y"> the tile Y in the grid [0..Rows-1] </param>
        /// <param name="width"> the tile width in pixels </param>
        /// <param name="height"> the tile height in pixels </param>
        /// <param name="pngBytes"> the PNG bytes </param>
        public void SetTile(int x, int y, int width, int height, byte[] pngBytes)
        {
            _tiles[x, y] = new PngTile
            {
                tileX = x,
                tileY = y,
                width = width,
                height = height,
                pngBytes = pngBytes
            };
        }

        /// <summary>
        /// Get the tile at (x,y).
        /// </summary>
        /// <param name="x"> the tile X in the grid [0..Cols-1] </param>
        /// <param name="y"> the tile Y in the grid [0..Rows-1] </param>
        /// <returns> the tile or null if not set </returns>
        public PngTile GetTile(int x, int y)
        {
            return _tiles[x, y];
        }

        /// <summary>
        /// Number of columns in the grid.
        /// </summary>
        public int Cols { get { return _tiles.GetLength(0); } }
        /// <summary>
        /// Number of rows in the grid.
        /// </summary>
        public int Rows { get { return _tiles.GetLength(1); } }

        /// <summary>
        /// Indexer to get/set tiles.
        /// </summary>
        /// <param name="x"> the tile X in the grid [0..Cols-1] </param>
        /// <param name="y"> the tile Y in the grid [0..Rows-1] </param>
        /// <returns> the tile </returns>
        public PngTile this[int x, int y]
        {
            get { return _tiles[x, y]; }
            set { _tiles[x, y] = value; }
        }
    }
}