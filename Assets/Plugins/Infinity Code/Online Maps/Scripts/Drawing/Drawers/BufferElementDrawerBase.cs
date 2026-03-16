/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Abstract base class for buffer-based drawing operations on map elements.
    /// </summary>
    public abstract class BufferElementDrawerBase : DrawingElementDrawerBase
    {
        private static Dictionary<Type, BufferElementDrawerBase> drawers = new Dictionary<Type, BufferElementDrawerBase>();

        /// <summary>
        /// Draws a map element onto the specified buffer using the appropriate buffer drawer.
        /// </summary>
        /// <param name="mapDrawer">The map drawer interface.</param>
        /// <param name="element">The drawing element to render.</param>
        /// <param name="buffer">The color buffer to draw into.</param>
        /// <param name="bufferPosition">The position of the buffer in tile coordinates.</param>
        /// <param name="bufferWidth">The width of the buffer in pixels.</param>
        /// <param name="bufferHeight">The height of the buffer in pixels.</param>
        /// <param name="zoom">The zoom level.</param>
        /// <param name="invertY">Whether to invert the Y axis.</param>
        public static void Draw(IMapDrawer mapDrawer, DrawingElement element, Color32[] buffer, Vector2 bufferPosition, int bufferWidth, int bufferHeight, float zoom, bool invertY = false)
        {
            GetDrawer(element)?.DrawElement(mapDrawer, element, buffer, bufferPosition, bufferWidth, bufferHeight, zoom, invertY);
        }

        /// <summary>
        /// Draws the specified drawing element onto the buffer.
        /// </summary>
        /// <param name="mapDrawer">The map drawer interface.</param>
        /// <param name="element">The drawing element to render.</param>
        /// <param name="buffer">The color buffer to draw into.</param>
        /// <param name="bufferPosition">The position of the buffer in tile coordinates.</param>
        /// <param name="bufferWidth">The width of the buffer in pixels.</param>
        /// <param name="bufferHeight">The height of the buffer in pixels.</param>
        /// <param name="zoom">The zoom level.</param>
        /// <param name="invertY">Whether to invert the Y axis.</param>
        /// <returns>True if the element was drawn; otherwise, false.</returns>
        protected abstract bool DrawElement(IMapDrawer mapDrawer, DrawingElement element, Color32[] buffer, Vector2 bufferPosition, int bufferWidth, int bufferHeight, float zoom, bool invertY = false);

        /// <summary>
        /// Gets the appropriate buffer drawer instance for the specified drawing element.
        /// </summary>
        /// <param name="element">The drawing element for which to get the buffer drawer.</param>
        /// <returns>The buffer drawer instance for the element type.</returns>
        public static BufferElementDrawerBase GetDrawer(DrawingElement element)
        {
            BufferElementDrawerBase drawer;
            if (drawers.TryGetValue(element.GetType(), out drawer)) return drawer;

            Type drawerType = element.bufferDrawerType;
            drawer = Activator.CreateInstance(drawerType) as BufferElementDrawerBase;
            drawers[element.GetType()] = drawer;
            return drawer;
        }
    }
    
    /// <summary>
    /// Generic abstract base class for buffer-based drawing operations for a specific DrawingElement type.
    /// </summary>
    /// <typeparam name="T">Type of the drawing element.</typeparam>
    public abstract class BufferDrawerBase<T> : BufferElementDrawerBase where T : DrawingElement
    {
        protected override bool DrawElement(IMapDrawer mapDrawer, DrawingElement element, Color32[] buffer, Vector2 bufferPosition, int bufferWidth, int bufferHeight, float zoom, bool invertY = false)
        {
            if (!Validate(mapDrawer, element)) return false;
            DrawElement(mapDrawer, element as T, buffer, bufferPosition, bufferWidth, bufferHeight, zoom, invertY);
            return true;
        }

        /// <summary>
        /// Draws the specified drawing element of type T onto the buffer.
        /// </summary>
        /// <param name="mapDrawer">The map drawer interface.</param>
        /// <param name="element">The drawing element to render.</param>
        /// <param name="buffer">The color buffer to draw into.</param>
        /// <param name="bufferPosition">The position of the buffer in tile coordinates.</param>
        /// <param name="bufferWidth">The width of the buffer in pixels.</param>
        /// <param name="bufferHeight">The height of the buffer in pixels.</param>
        /// <param name="zoom">The zoom level.</param>
        /// <param name="invertY">Whether to invert the Y axis.</param>
        protected abstract void DrawElement(IMapDrawer mapDrawer, T element, Color32[] buffer, Vector2 bufferPosition, int bufferWidth, int bufferHeight, float zoom, bool invertY = false);

        /// <summary>
        /// Draws a polyline to the buffer for the specified element.
        /// </summary>
        /// <param name="element">The drawing element containing the polyline points.</param>
        /// <param name="buffer">The color buffer to draw into.</param>
        /// <param name="bufferPosition">The position of the buffer in tile coordinates.</param>
        /// <param name="bufferSize">The size of the buffer in pixels.</param>
        /// <param name="zoom">The zoom level.</param>
        /// <param name="color">The color of the line.</param>
        /// <param name="width">The width of the line in pixels.</param>
        /// <param name="closed">Whether the polyline should be closed (forms a polygon).</param>
        /// <param name="invertY">Whether to invert the Y axis.</param>
        protected void DrawLineToBuffer(T element, Color32[] buffer, Vector2 bufferPosition, Vector2Int bufferSize, float zoom, Color32 color, float width, bool closed, bool invertY)
        {
            if (color.a == 0) return;

            int intZoom = (int) zoom;
            float zoomScale = Mathf.Pow(2, intZoom - zoom);

            TilePoint t1, t2;
            int w = Mathf.RoundToInt(width);

            double ppx1 = 0;

            float bx1 = bufferPosition.x;
            float bx2 = bx1 + zoomScale * bufferSize.x / Constants.TileSize;
            float by1 = bufferPosition.y;
            float by2 = by1 + zoomScale * bufferSize.y / Constants.TileSize;
            TileRect bufferRect = new TileRect(bx1, by1, bx2, by2, intZoom);
            
            MercatorPoint[] mercatorPoints = element.mercatorPoints;

            for (int i = 1; i < mercatorPoints.Length; i++)
            {
                (t1, t2) = GetSegmentTilePoints(mercatorPoints, i - 1, intZoom); 
                DrawLinePartToBuffer(buffer, bufferSize, bufferRect, color, t1, t2, ref ppx1, w, invertY, zoomScale);
            }

            if (!closed) return;

            (t1, t2) = GetSegmentTilePoints(mercatorPoints, mercatorPoints.Length - 1, intZoom);
            DrawLinePartToBuffer(buffer, bufferSize, bufferRect, color, t1, t2, ref ppx1, w, invertY, zoomScale);
        }

        private static void DrawLinePartToBuffer(Color32[] buffer, Vector2Int bufferSize, TileRect bufferRect, Color32 color, TilePoint t1, TilePoint t2, ref double ppx1, int w, bool invertY, float zoomScale)
        {
            if (t1.y < bufferRect.top && t2.y < bufferRect.top) return;
            if (t1.y > bufferRect.bottom && t2.y > bufferRect.bottom) return;
            
            if ((t1 - t2).sqrMagnitude > 0.04)
            {
                TilePoint ct = TilePoint.Lerp(t1, t2, 0.5);
                DrawLinePartToBuffer(buffer, bufferSize, bufferRect, color, t1, ct, ref ppx1, w, invertY, zoomScale);
                DrawLinePartToBuffer(buffer, bufferSize, bufferRect, color, ct, t2, ref ppx1, w, invertY, zoomScale);
                return;
            }
            
            if (!bufferRect.ContainsWrapped(t1) && !bufferRect.ContainsWrapped(t2)) return;

            int maxTiles = t1.maxTiles;
            TilePoint centerTile = new TilePoint(maxTiles >> 1, maxTiles >> 1, t1.zoom);
            double mt = maxTiles / zoomScale;

            t1 -= centerTile;
            t2 -= centerTile;
            
            double gpx1 = t1.x + maxTiles;
            double lpx1 = t1.x - maxTiles;

            if (Math.Abs(ppx1 - gpx1) < Math.Abs(ppx1 - t1.x)) t1.x = gpx1;
            else if (Math.Abs(ppx1 - lpx1) < Math.Abs(ppx1 - t1.x)) t1.x = lpx1;

            ppx1 = t1.x;

            double gpx2 = t2.x + maxTiles;
            double lpx2 = t2.x - maxTiles;

            if (Math.Abs(ppx1 - gpx2) < Math.Abs(ppx1 - t2.x)) t2.x = gpx2;
            else if (Math.Abs(ppx1 - lpx2) < Math.Abs(ppx1 - t2.x)) t2.x = lpx2;

            TilePoint p1 = (t1 + centerTile - bufferRect.topLeft) / zoomScale;
            TilePoint p2 = (t2 + centerTile - bufferRect.topLeft) / zoomScale;

            if (p1.x > mt && p2.x > mt)
            {
                p1.x -= mt;
                p2.x -= mt;
            }

            p1 *= Constants.TileSize;
            p2 *= Constants.TileSize;

            double stX = Mathd.Clamp(Math.Min(p1.x, p2.x) - w, bufferSize.x);
            double stY = Mathd.Clamp(Math.Min(p1.y, p2.y) - w, bufferSize.y);
            double endX = Mathd.Clamp(Math.Max(p1.x, p2.x) + w, bufferSize.x);
            double endY = Mathd.Clamp(Math.Max(p1.y, p2.y) + w, bufferSize.y);

            int istx = (int) Math.Round(stX);
            int isty = (int) Math.Round(stY);

            int sqrW = w * w;

            int lengthX = (int) Math.Round(endX - stX);
            int lengthY = (int) Math.Round(endY - stY);

            byte clrR = color.r;
            byte clrG = color.g;
            byte clrB = color.b;
            byte clrA = color.a;
            float alpha = clrA / 255f;

            for (int y = 0; y < lengthY; y++)
            {
                double py = y + stY;
                int ipy = y + isty;
                double centerY = py + 0.5;
                if (!invertY) ipy = bufferSize.y - ipy - 1;
                ipy *= bufferSize.x;

                for (int x = 0; x < lengthX; x++)
                {
                    double px = x + stX;
                    double centerX = px + 0.5;

                    double npx, npy;

                    Geometry.NearestPointStrict(centerX, centerY, p1.x, p1.y, p2.x, p2.y, out npx, out npy);
                    double onpx = centerX - npx;
                    double onpy = centerY - npy;

                    double dist = onpx * onpx + onpy * onpy;

                    if (dist > sqrW) continue;

                    int bufferIndex = ipy + x + istx;
                    Color32 pc = buffer[bufferIndex];
                    pc.r = (byte)((clrR - pc.r) * alpha + pc.r);
                    pc.g = (byte)((clrG - pc.g) * alpha + pc.g);
                    pc.b = (byte)((clrB - pc.b) * alpha + pc.b);
                    pc.a = (byte)((clrA - pc.a) * alpha + pc.a);
                    buffer[bufferIndex] = pc;
                }
            }
        }

        /// <summary>
        /// Fills a polygon defined by the specified element onto the buffer with the given color.
        /// </summary>
        /// <param name="element">The drawing element containing the polygon points.</param>
        /// <param name="buffer">The color buffer to fill into.</param>
        /// <param name="bufferPosition">The position of the buffer in tile coordinates.</param>
        /// <param name="bufferWidth">The width of the buffer in pixels.</param>
        /// <param name="bufferHeight">The height of the buffer in pixels.</param>
        /// <param name="zoom">The zoom level.</param>
        /// <param name="color">The fill color.</param>
        /// <param name="invertY">Whether to invert the Y axis.</param>
        protected void FillPoly(T element, Color32[] buffer, Vector2 bufferPosition, int bufferWidth, int bufferHeight, float zoom, Color32 color, bool invertY)
        {
            if (color.a == 0) return;
            float alpha = color.a / 255f;

            double minX, maxX, minY, maxY;
            double[] bufferPoints = GetBufferPoints(element, bufferPosition, zoom, out minX, out maxX, out minY, out maxY);

            if (maxX < 0 || minX > bufferWidth || maxY < 0 || minY > bufferHeight) return;

            double stX = minX;
            if (stX < 0) stX = 0;
            else if (stX > bufferWidth) stX = bufferWidth;

            double stY = minY;
            if (stY < 0) stY = 0;
            else if (stY > bufferHeight) stY = bufferHeight;

            double endX = maxX;
            if (endX < 0) stX = 0;
            else if (endX > bufferWidth) endX = bufferWidth;

            double endY = maxY;
            if (endY < 0) endY = 0;
            else if (endY > bufferHeight) endY = bufferHeight;

            int lengthX = (int)Math.Round(endX - stX);
            int lengthY = (int)Math.Round(endY - stY);

            Color32 clr = new Color32(color.r, color.g, color.b, 255);

            const int blockSize = 5;
            int blockCountX = lengthX / blockSize + (lengthX % blockSize == 0 ? 0 : 1);
            int blockCountY = lengthY / blockSize + (lengthY % blockSize == 0 ? 0 : 1);

            byte clrR = clr.r;
            byte clrG = clr.g;
            byte clrB = clr.b;

            int istx = (int) Math.Round(stX);
            int isty = (int) Math.Round(stY);

            for (int by = 0; by < blockCountY; by++)
            {
                int byp = by * blockSize;
                double bufferY = byp + stY;
                int iby = byp + isty;

                for (int bx = 0; bx < blockCountX; bx++)
                {
                    int bxp = bx * blockSize;
                    double bufferX = bxp + stX;
                    int ibx = bxp + istx;

                    bool p1 = Geometry.IsPointInPolygon(bufferPoints, bufferX, bufferY);
                    bool p2 = Geometry.IsPointInPolygon(bufferPoints, bufferX + blockSize - 1, bufferY);
                    bool p3 = Geometry.IsPointInPolygon(bufferPoints, bufferX + blockSize - 1, bufferY + blockSize - 1);
                    bool p4 = Geometry.IsPointInPolygon(bufferPoints, bufferX, bufferY + blockSize - 1);

                    if (p1 && p2 && p3 && p4)
                    {
                        for (int y = 0; y < blockSize; y++)
                        {
                            if (byp + y >= lengthY) break;
                            int cby = iby + y;
                            if (!invertY) cby = bufferHeight - cby - 1;
                            int byi = cby * bufferWidth + ibx;

                            for (int x = 0; x < blockSize; x++)
                            {
                                if (bxp + x >= lengthX) break;

                                int bufferIndex = byi + x;
                            
                                Color32 a = buffer[bufferIndex];
                                a.r = (byte) (a.r + (clrR - a.r) * alpha);
                                a.g = (byte) (a.g + (clrG - a.g) * alpha);
                                a.b = (byte) (a.b + (clrB - a.b) * alpha);
                                a.a = (byte) (a.a + (255 - a.a) * alpha);
                                buffer[bufferIndex] = a;
                            }
                        }
                    }
                    else if (p1 || p2 || p3 || p4)
                    {
                        for (int y = 0; y < blockSize; y++)
                        {
                            if (byp + y >= lengthY) break;
                            int cby = iby + y;
                            if (!invertY) cby = bufferHeight - cby - 1;
                            int byi = cby * bufferWidth + ibx;

                            for (int x = 0; x < blockSize; x++)
                            {
                                if (bxp + x >= lengthX) break;

                                if (!Geometry.IsPointInPolygon(bufferPoints, bufferX + x, bufferY + y)) continue;
                                
                                int bufferIndex = byi + x;
                                Color32 a = buffer[bufferIndex];
                                a.r = (byte)(a.r + (clrR - a.r) * alpha);
                                a.g = (byte)(a.g + (clrG - a.g) * alpha);
                                a.b = (byte)(a.b + (clrB - a.b) * alpha);
                                a.a = (byte)(a.a + (255 - a.a) * alpha);
                                buffer[bufferIndex] = a;
                            }
                        }
                    }
                }
            }
        }

        private double[] GetBufferPoints(T element, Vector2 bufferPosition, float zoom, out double minX, out double maxX, out double minY, out double maxY)
        {
            int izoom = (int) zoom;
            float zoomScale = Mathf.Pow(2, izoom - zoom);
            float scaledTileSize = Constants.TileSize / zoomScale;
            
            MercatorPoint[] mercatorPoints = element.mercatorPoints;
            double[] bufferPoints = new double[mercatorPoints.Length * 2];

            minX = double.MaxValue;
            maxX = double.MinValue;
            minY = double.MaxValue;
            maxY = double.MinValue;

            for (int i = 0; i < mercatorPoints.Length; i++)
            {
                TilePoint t = mercatorPoints[i].ToTile(izoom);
                t -= bufferPosition;
                t *= scaledTileSize;

                if (t.x < minX) minX = t.x;
                if (t.x > maxX) maxX = t.x;
                if (t.y < minY) minY = t.y;
                if (t.y > maxY) maxY = t.y;

                bufferPoints[i * 2] = t.x;
                bufferPoints[i * 2 + 1] = t.y;
            }
            
            return bufferPoints;
        }

        private (TilePoint, TilePoint) GetSegmentTilePoints(MercatorPoint[] mercatorPoints, int index, int zoom)
        {
            MercatorPoint p1 = mercatorPoints[index % mercatorPoints.Length];
            MercatorPoint p2 = mercatorPoints[(index + 1) % mercatorPoints.Length];
            TilePoint t1 = p1.ToTile(zoom);
            TilePoint t2 = p2.ToTile(zoom);

            double ox = p1.x - p2.x;
                
            if (ox > 0.5) t2.x += 1 << zoom;
            else if (ox < -0.5) t2.x -= 1 << zoom;

            return (t1, t2);
        }
    }
}