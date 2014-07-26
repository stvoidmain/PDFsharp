using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MigraDoc.DocumentObjectModel;
using PdfSharp.Drawing;

namespace MigraDoc.Rendering
{
    internal class CellTopDownFormatter
    {
        internal CellTopDownFormatter( FormattedCell areaProvider, DocumentRenderer documentRenderer, DocumentElements elements )
        {
            this.documentRenderer = documentRenderer;
            this.areaProvider = areaProvider;
            this.elements = elements;
        }

        public void Format( XGraphics graphics )
        {
            gfx = graphics;
            Area area = areaProvider.GetNextArea();
            XUnit prevBottomMargin = 0;
            XUnit yPos = prevBottomMargin;
            RenderInfo prevRenderInfo = null;
            FormatInfo prevFormatInfo = null;
            var renderInfos = new List<RenderInfo>();
            bool ready = this.elements.Count == 0;
            bool isFirstOnPage = true;
            XUnit maxHeight = area.Height;
            foreach ( DocumentObject item in elements )
            {
                var maxBottom = item.Section.PageSetup.PageHeight.Point - item.Section.PageSetup.TopMargin.Point;
                var renderer = Renderer.Create( gfx, documentRenderer, item, areaProvider.AreaFieldInfos );
                if ( prevFormatInfo == null )
                {
                    LayoutInfo initialLayoutInfo = renderer.InitialLayoutInfo;
                    XUnit distance = prevBottomMargin;
                    if ( initialLayoutInfo.VerticalReference == VerticalReference.PreviousElement &&
                        initialLayoutInfo.Floating != Floating.None ) //Added KlPo 12.07.07
                        distance = MarginMax( initialLayoutInfo.MarginTop, distance );

                    area = area.Lower( distance );
                }
                renderer.Format( area, prevFormatInfo );
                if ( renderer.RenderInfo.LayoutInfo.ContentArea.Y > maxBottom )
                {

                }
            }
        }

        /// <summary>
        /// Returns the max of the given Margins, if both are positive or 0, the sum otherwise.
        /// </summary>
        /// <param name="prevBottomMargin">The bottom margin of the previous element.</param>
        /// <param name="nextTopMargin">The top margin of the next element.</param>
        /// <returns></returns>
        private XUnit MarginMax( XUnit prevBottomMargin, XUnit nextTopMargin )
        {
            if ( prevBottomMargin >= 0 && nextTopMargin >= 0 )
                return Math.Max( prevBottomMargin, nextTopMargin );
            else
                return prevBottomMargin + nextTopMargin;
        }

        private IAreaProvider areaProvider;
        private DocumentElements elements;
        private DocumentRenderer documentRenderer;
        private XGraphics gfx;

    }
}
