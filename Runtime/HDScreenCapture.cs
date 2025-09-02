using Hjg.Pngcs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HDSC
{
    public class HDScreenCapture : MonoBehaviour
    {
        public static HDScreenCapture Instance;
        public int MaxTileSide = 3072;
        private int _nbTotalTiles;
        private int _nbTilesDone;
        private int _nbTotalRows;
        private int _nbRowsDone;

        private void Awake()
        {
            // insure singleton instance
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
        }

        /// <summary>
        /// Render a high-resolution screenshot from the specified camera.
        /// </summary>
        /// <param name="camera"> Camera to capture the screenshot from </param>
        /// <param name="width"> Desired screenshot width in pixels </param>
        /// <param name="height"> Desired screenshot height in pixels </param>
        /// <param name="supersample"> Whether to apply 2x supersampling for improved quality </param>
        /// <param name="transparentBG"> Whether to render with a transparent background </param>
        /// <param name="pngCompression"> PNG compression level (0-9) </param>
        /// <param name="onRendered"> Callback invoked with the PNG byte array when rendering is complete </param>
        /// <param name="onRendering"> Callback invoked with progress updates: (stage, progress) where stage is 0 for capturing tiles and 1 for merging </param>
        public void Capture(Camera camera, int width, int height, bool supersample, bool transparentBG, int pngCompression, Action<byte[]> onRendered, Action<int, float> onRendering)
        {
            StartCoroutine(GenerateTiles(camera, width, height, MaxTileSide, supersample ? 2 : 1, transparentBG,
                (tiles) =>
                {
                    onRendering?.Invoke(1, 0);
                    StartCoroutine(MergeTiles(tiles, onRendered, pngCompression, (progress) => onRendering?.Invoke(1, progress)));
                },
                (progress) => onRendering?.Invoke(0, progress)));
        }

        /// <summary>
        /// Get recommended screenshot resolutions based on common aspect ratios and device limits.
        /// </summary>
        /// <returns> Array of recommended resolutions </returns>
        public List<ScreenshotResolutions> GetCommonResolutions(int maxK = 132)
        {
            List<ScreenshotResolutions> res = new List<ScreenshotResolutions>
            {
                // Add common resolutions from 720p to 131k
                new ScreenshotResolutions(1280, 720),
                new ScreenshotResolutions(1920, 1080),
                new ScreenshotResolutions(2048, 1152),
                new ScreenshotResolutions(2560, 1440),
                new ScreenshotResolutions(3840, 2160),
                new ScreenshotResolutions(4096, 2160),
                new ScreenshotResolutions(5120, 2880),
                new ScreenshotResolutions(7680, 4320),
                new ScreenshotResolutions(8192, 4320),
                new ScreenshotResolutions(10240, 5760),
                new ScreenshotResolutions(15360, 8640),
                new ScreenshotResolutions(16384, 8640),
                new ScreenshotResolutions(20480, 11520),
                new ScreenshotResolutions(30720, 17280),
                new ScreenshotResolutions(32768, 17280),
                new ScreenshotResolutions(40960, 21600),
                new ScreenshotResolutions(61440, 32400),
                new ScreenshotResolutions(65536, 32768),
                new ScreenshotResolutions(81920, 40960),
                new ScreenshotResolutions(102400, 51200),
                new ScreenshotResolutions(131072, 65536),
            };

            // Filter out resolutions that exceed the maximum allowed size
            maxK *= 1000;
            maxK = Math.Min(maxK, 131072); // Cap at 131k
            res.RemoveAll(r => r.width > maxK);

            return res;
        }

        /// <summary>
        /// Render a camera in tiles to produce a very high-res PNG (with alpha if needed).
        /// Tiles are rendered one-by-one and passed as PNG byte arrays to onTilePng(tx,ty,png).
        /// </summary>
        /// <param name="srcCamera"> The camera to render from </param>
        /// <param name="finalWidth"> the width of the resulting PNG </param>
        /// <param name="finalHeight"> the height of the resulting PNG </param>
        /// <param name="maxTileSide"> max tile size in pixels (e.g. 2048 or 4096) </param>
        /// <param name="supersample"> 1 = native, 2/4 = render bigger then downscale (best AA on WebGL) </param>
        /// <param name="transparentBG"> if true, background alpha = 0 (no skybox) </param>
        /// <param name="onAllDone"> Callback when all tiles done </param>
        /// <param name="OnLoadingProgress"> Callback for progress [0..1] </param>
        public IEnumerator GenerateTiles(Camera srcCamera, int finalWidth, int finalHeight, int maxTileSide, int supersample, bool transparentBG, Action<PngTiles> onAllDone, Action<float> OnLoadingProgress)
        {
            _nbTilesDone = 0;
            _nbTotalTiles = 1;

            if (finalWidth <= 0 || finalHeight <= 0) yield break;
            supersample = Mathf.Max(1, supersample);

            // Device caps
            int capRT = Mathf.Max(256, SystemInfo.maxTextureSize);
            int tileCap = Mathf.Max(256, capRT / supersample); // make sure hi-res RT fits
            int tileSide = Mathf.Clamp(maxTileSide, 256, tileCap);

            // Grid
            int cols = Mathf.CeilToInt(finalWidth / (float)tileSide);
            int rows = Mathf.CeilToInt(finalHeight / (float)tileSide);
            float targetAspect = finalWidth / (float)finalHeight;

            _nbTotalTiles = rows * cols;
            PngTiles allTiles = new PngTiles(cols, rows, finalWidth, finalHeight);

            // Duplicate camera (isolated state)
            var go = new GameObject("TiledScreenshotCamera");
            go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(srcCamera);
            cam.enabled = false;

            // Skybox / background
            if (transparentBG)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
            }
            else
            {
                cam.clearFlags = CameraClearFlags.Skybox;
            }

            // Loop tiles
            for (int ty = 0; ty < rows; ty++)
            {
                for (int tx = 0; tx < cols; tx++)
                {
                    // Tile size (edge tiles may be smaller)
                    int x0 = tx * tileSide;
                    int y0 = ty * tileSide;
                    int w = Mathf.Min(tileSide, finalWidth - x0);
                    int h = Mathf.Min(tileSide, finalHeight - y0);

                    // Normalized bounds in final image [0..1]
                    float u0 = x0 / (float)finalWidth;
                    float v0 = y0 / (float)finalHeight;
                    float u1 = (x0 + w) / (float)finalWidth;
                    float v1 = (y0 + h) / (float)finalHeight;

                    // Map to NDC [-1..1]
                    float nx0 = u0 * 2f - 1f;
                    float ny0 = v0 * 2f - 1f;
                    float nx1 = u1 * 2f - 1f;
                    float ny1 = v1 * 2f - 1f;

                    // Off-center projection for this tile (persp or ortho)
                    Matrix4x4 tileProj = cam.orthographic
                        ? BuildOrthoOffCenter(cam, nx0, nx1, ny0, ny1, targetAspect)
                        : BuildPerspectiveOffCenter(cam, nx0, nx1, ny0, ny1, targetAspect);

                    cam.projectionMatrix = tileProj;

                    // Hi-res RT (supersample)
                    int rw = w * supersample;
                    int rh = h * supersample;

                    var rtHi = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32)
                    {
                        antiAliasing = 1, // MSAA unreliable on WebGL RTs; prefer supersampling
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };

                    RenderTexture rtFinal = null;

                    // Render
                    cam.targetTexture = rtHi;
                    yield return new WaitForEndOfFrame();
                    cam.Render();

                    // Downscale to final tile size (bilinear) if supersampling
                    if (supersample > 1)
                    {
                        rtFinal = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
                        {
                            wrapMode = TextureWrapMode.Clamp,
                            filterMode = FilterMode.Bilinear
                        };
                        Graphics.Blit(rtHi, rtFinal);
                        yield return null;
                    }
                    else
                    {
                        rtFinal = rtHi;
                    }

                    // Readback
                    RenderTexture.active = rtFinal;
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                    tex.Apply(false, false);
                    RenderTexture.active = null;
                    yield return null;

                    // PNG bytes
                    byte[] png = ImageConversion.EncodeToPNG(tex);

                    // Cleanup per tile
                    UnityEngine.Object.Destroy(rtHi);
                    if (rtFinal != rtHi) UnityEngine.Object.Destroy(rtFinal);
                    UnityEngine.Object.Destroy(tex);

                    // Store the tile
                    allTiles.SetTile(tx, (rows - 1) - ty, w, h, png); // flip Y for usual image coords

                    // Progress handler
                    _nbTilesDone++;
                    OnLoadingProgress?.Invoke(_nbTilesDone / (float)_nbTotalTiles);
                }
            }

            UnityEngine.Object.Destroy(go);
            onAllDone?.Invoke(allTiles);
        }

        /// <summary>
        /// Perspective off-center from NDC box and target aspect
        /// </summary>
        /// <param name="cam"> The camera </param>
        /// <param name="nx0"> the left in NDC [-1..1] </param>
        /// <param name="nx1"> the right in NDC [-1..1] </param>
        /// <param name="ny0"> the bottom in NDC [-1..1] </param>
        /// <param name="ny1"> the top in NDC [-1..1] </param>
        /// <param name="targetAspect"> the final target aspect ratio (not cam.aspect) </param>
        /// <returns> the off-center projection matrix </returns>
        private Matrix4x4 BuildPerspectiveOffCenter(Camera cam, float nx0, float nx1, float ny0, float ny1, float targetAspect)
        {
            float near = cam.nearClipPlane;
            float far = cam.farClipPlane;

            float tan = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float top = tan * near;
            float bottom = -top;

            // IMPORTANT: use the final target aspect (not cam.aspect)
            float right = top * targetAspect;
            float left = -right;

            float l = Mathf.Lerp(left, right, (nx0 + 1f) * 0.5f);
            float r = Mathf.Lerp(left, right, (nx1 + 1f) * 0.5f);
            float b = Mathf.Lerp(bottom, top, (ny0 + 1f) * 0.5f);
            float t = Mathf.Lerp(bottom, top, (ny1 + 1f) * 0.5f);

            return Matrix4x4.Frustum(l, r, b, t, near, far);
        }

        /// <summary>
        /// Orthographic off-center from NDC box and target aspect
        /// </summary>
        /// <param name="cam"> The camera </param>
        /// <param name="nx0"> the left in NDC [-1..1] </param>
        /// <param name="nx1"> the right in NDC [-1..1] </param>
        /// <param name="ny0"> the bottom in NDC [-1..1] </param>
        /// <param name="ny1"> the top in NDC [-1..1] </param>
        /// <param name="targetAspect"> the final target aspect ratio (not cam.aspect) </param>
        /// <returns> the off-center projection matrix </returns>
        private Matrix4x4 BuildOrthoOffCenter(Camera cam, float nx0, float nx1, float ny0, float ny1, float targetAspect)
        {
            float near = cam.nearClipPlane;
            float far = cam.farClipPlane;

            float halfH = cam.orthographicSize;
            float halfW = halfH * targetAspect;

            float l = Mathf.Lerp(-halfW, halfW, (nx0 + 1f) * 0.5f);
            float r = Mathf.Lerp(-halfW, halfW, (nx1 + 1f) * 0.5f);
            float b = Mathf.Lerp(-halfH, halfH, (ny0 + 1f) * 0.5f);
            float t = Mathf.Lerp(-halfH, halfH, (ny1 + 1f) * 0.5f);

            return Matrix4x4.Ortho(l, r, b, t, near, far);
        }

        /// <summary>
        /// Merge a grid of PNG tiles (with optional overlap) into one big PNG with alpha.
        /// Tiles are read row-by-row to keep memory low. Pure C# (Pngcs), WebGL-friendly.
        /// Tile files are discovered via tilePathProvider(row, col) -> absolute or persistentDataPath.
        /// </summary>
        public IEnumerator MergeTiles(PngTiles tiles, Action<byte[]> onDone, int pngCompressionLevel, Action<float> onLoadingProgress)
        {
            _nbRowsDone = 0;
            _nbTotalRows = tiles.FinalHeight;
            var imgInfoOut = new ImageInfo(tiles.FinalWidth, tiles.FinalHeight, 8, true);
            using (var outStream = new MemoryStream())
            {
                var writer = new PngWriter(outStream, imgInfoOut);
                writer.CompLevel = pngCompressionLevel;
                writer.SetFilterType(FilterType.FILTER_DEFAULT);

                int outRowIndex = 0;

                for (int ty = 0; ty < tiles.Rows; ty++)
                {
                    var readers = new PngReader[tiles.Cols];
                    var nextRow = new int[tiles.Cols];      // next row to read for each tile
                    var wOv = new int[tiles.Cols];
                    var hOv = new int[tiles.Cols];

                    // --- open all tiles for this row ---
                    for (int tx = 0; tx < tiles.Cols; tx++)
                    {
                        byte[] pngData = tiles.GetTile(tx, ty).pngBytes;
                        Stream pngStream = new MemoryStream(pngData);
                        var r = readers[tx] = new PngReader(pngStream);
                        wOv[tx] = r.ImgInfo.Cols;
                        hOv[tx] = r.ImgInfo.Rows;

                        int vOverlap = 0; // TODO: set to your vertical overlap in rows if you have any
                        if (ty > 0 && vOverlap > 0)
                        {
                            int toSkip = Math.Min(vOverlap, hOv[tx] - nextRow[tx]);
                            for (int i = 0; i < toSkip; i++) r.ReadRow(nextRow[tx]++);
                        }
                    }

                    // FIX: compute visible height = min(rows left in all tiles, rows left in output)
                    int minAvail = int.MaxValue;
                    for (int tx = 0; tx < tiles.Cols; tx++)
                        minAvail = Math.Min(minAvail, hOv[tx] - nextRow[tx]);

                    int visibleH = Math.Max(0, Math.Min(minAvail, tiles.FinalHeight - outRowIndex));

                    // write visible scanlines of this tile-row
                    for (int vy = 0; vy < visibleH; vy++)
                    {
                        var outLine = new ImageLine(imgInfoOut, ImageLine.ESampleType.INT);
                        int[] dst = outLine.Scanline;
                        int dstOfs = 0;

                        for (int tx = 0; tx < tiles.Cols; tx++)
                        {
                            var r = readers[tx];

                            // IMPORTANT: use the overload that returns the filled ImageLine
                            ImageLine inLine = r.ReadRow(nextRow[tx]++);
                            int[] src = inLine.Scanline;
                            int countInts = wOv[tx] * r.ImgInfo.Channels;
                            Buffer.BlockCopy(src, 0, dst, dstOfs * sizeof(int), countInts * sizeof(int));
                            dstOfs += countInts;
                        }

                        writer.WriteRow(outLine, outRowIndex++);
                        _nbRowsDone = outRowIndex;
                        onLoadingProgress?.Invoke(_nbRowsDone / (float)_nbTotalRows);
                        if (outRowIndex % 128 == 0) yield return null;
                    }

                    // close
                    for (int tx = 0; tx < tiles.Cols; tx++)
                    {
                        readers[tx].End();
                    }

                    yield return null;
                }

                writer.End();
                yield return null;
                byte[] resultPng = outStream.ToArray();
                yield return null;
                onDone?.Invoke(resultPng);
            }
        }
    }
}