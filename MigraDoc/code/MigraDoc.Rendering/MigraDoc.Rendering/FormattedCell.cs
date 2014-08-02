#region MigraDoc - Creating Documents on the Fly
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@pdfsharp.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.pdfsharp.com
// http://www.migradoc.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Linq;
using System.Collections;
using MigraDoc.DocumentObjectModel;
using PdfSharp.Drawing;
using MigraDoc.DocumentObjectModel.Tables;
using System.Collections.Generic;

namespace MigraDoc.Rendering
{
    /// <summary>
    /// Represents a formatted cell.
    /// </summary>
    internal class FormattedCell : IAreaProvider
    {
        internal FormattedCell( Cell cell, DocumentRenderer documentRenderer, Borders cellBorders, FieldInfos fieldInfos, XUnit xOffset, XUnit yOffset )
        {
            this.cell = cell;
            this.fieldInfos = fieldInfos;
            this.yOffset = yOffset;
            this.xOffset = xOffset;
            bordersRenderer = new BordersRenderer( cellBorders, null );
            this.documentRenderer = documentRenderer;
            renderInfos = new List<RenderInfo>();
            Constrain = new Rectangle( xOffset, yOffset, 0, double.MaxValue );
        }

        public bool Done { get; private set; }

        private Area initialRect;
        private Area constrain;
        internal Area Constrain
        {
            get { return constrain; }
            set
            {
                constrain = value;
                initialRect = new Rectangle( 0, 0, value.Width, value.Height );
            }
        }
        bool isFirstArea = true;
        Area IAreaProvider.GetNextArea()
        {
            var rect = CalcContentRect();
            if ( isFirstArea )
            {
                isFirstArea = false;
                return rect;
            }
            else if ( isReFormat )
            {
                isReFormat = false;
                return rect;
            }
            return null;
        }

        Area IAreaProvider.ProbeNextArea()
        {
            //if ( renderInfos.Count > 0 )
            //{
            //    var ch = CalcContentHeight( documentRenderer );
            //    var low = Constrain.Lower( ch );
            //    if ( low.Height > 0 )
            //    {
            //        return new Rectangle( xOffset, yOffset, 0, low.Height );
            //    }
            //}
            return null;// new Rectangle( xOffset, yOffset, 0, Constrain.Height );
        }

        private int lastIndex;

        internal void Format( XGraphics gfx )
        {
            var isRe = isReFormat;
            this.gfx = gfx;
            formatter = new TopDownFormatter( this, documentRenderer, cell.Elements );
            formatter.FormatOnAreas( gfx, false, lastIndex, lastRenderInfo );
            contentHeight = CalcContentHeight( documentRenderer );
            Done = formatter.LastIndex >= cell.Elements.Count && formatter.LastPrevRenderInfo == null && contentHeight < Constrain.Height;
            lastIndex = formatter.LastIndex;
            lastRenderInfo = formatter.LastPrevRenderInfo;
            if ( !isRe )
            {
                lastIndex = 0;
                lastRenderInfo = null;
                //renderInfos.Clear();
            }
        }

        private FormatInfo LastFormatInfo
        {
            get
            {
                return lastRenderInfo != null ? lastRenderInfo.FormatInfo : null;
            }
        }

        private bool isReFormat;
        internal void ReFormat( XGraphics gfx, bool overrideFormat = false )
        {
            if ( Done && !overrideFormat )
            {
                return;
            }
            if ( lastIndex > 0 && lastIndex == cell.Elements.Count )
            {
                lastIndex--;
            }
            //var obByIndex = cell.Elements[ lastIndex ];
            //if ( lastRenderInfo != null )
            //{
            //    if ( lastRenderInfo.DocumentObject != obByIndex )
            //    {
            //        lastRenderInfo = null;
            //        foreach ( var ri in renderInfos )
            //        {
            //            if ( ri.DocumentObject == obByIndex )
            //            {
            //                lastRenderInfo = ri;
            //            }
            //        }
            //    }
            //}
            //else
            //{
            //    foreach ( var ri in renderInfos )
            //    {
            //        if ( ri.DocumentObject == obByIndex && !ri.FormatInfo.IsComplete )
            //        {
            //            lastRenderInfo = ri;
            //        }
            //    }
            //}
            //if ( lastRenderInfo != null && ( lastRenderInfo.FormatInfo.IsComplete || lastRenderInfo.FormatInfo.IsEmpty ) )
            //{
            //    lastRenderInfo = null;
            //}
            renderInfos = new List<RenderInfo>();
            isReFormat = true;
            Format( gfx );
            isReFormat = false;
        }

        private Rectangle CalcContentRect()
        {
            Column column = cell.Column;
            XUnit width = InnerWidth;
            width -= column.LeftPadding.Point;
            Column rightColumn = cell.Table.Columns[ column.Index + cell.MergeRight ];
            width -= rightColumn.RightPadding.Point;

            XUnit height = double.MaxValue;// isFirstArea ? Constrain.Height.Point : double.MaxValue;
            return new Rectangle( xOffset, yOffset, width, Constrain.Height.Point );
        }

        internal XUnit ContentHeight
        {
            get { return contentHeight; }
        }

        internal XUnit InnerHeight
        {
            get
            {
                Row row = cell.Row;
                XUnit verticalPadding = row.TopPadding.Point;
                verticalPadding += row.BottomPadding.Point;
                switch ( row.HeightRule )
                {
                    case RowHeightRule.Exactly:
                        return row.Height.Point;

                    case RowHeightRule.Auto:
                        return verticalPadding + contentHeight;

                    case RowHeightRule.AtLeast:
                    default:
                        return Math.Max( row.Height, verticalPadding + contentHeight );
                        //return Math.Max( row.Height, Math.Min( Constrain.Height, verticalPadding + this.contentHeight ) );
                }
            }
        }

        internal XUnit InnerWidth
        {
            get
            {
                XUnit width = 0;
                int cellColumnIdx = cell.Column.Index;
                for ( int toRight = 0; toRight <= cell.MergeRight; ++toRight )
                {
                    int columnIdx = cellColumnIdx + toRight;
                    width += cell.Table.Columns[ columnIdx ].Width;
                }
                width -= bordersRenderer.GetWidth( BorderType.Right );

                return width;
            }
        }

        FieldInfos IAreaProvider.AreaFieldInfos
        {
            get
            {
                return fieldInfos;
            }
        }

        void IAreaProvider.StoreRenderInfos( IEnumerable<RenderInfo> renderInfos )
        {
            StoreRenderInfos( renderInfos );
        }

        internal void StoreRenderInfos( IEnumerable<RenderInfo> renderInfos, bool overwrite = false )
        {
            if ( overwrite )
            {
                this.renderInfos = new List<RenderInfo>( renderInfos );
                return;
            }
            if ( renderInfos != null )
            {
                foreach ( RenderInfo ri in renderInfos )
                {
                    if ( !this.renderInfos.Contains( ri ) )
                    {
                        this.renderInfos.Add( ri );
                    }
                }
            }
#if DEBUG
            if ( this.renderInfos != null && this.renderInfos.Any( r => r is TableRenderInfo ) )
            {
                foreach ( var tri in this.renderInfos.Where( r => r is TableRenderInfo ).Cast<TableRenderInfo>() )
                {
                    var fri = tri.FormatInfo as TableFormatInfo;
                    Console.WriteLine( "FormattedCell -> StoreRenderInfos: {0}", fri );
                }
            }
#endif
        }

        bool IAreaProvider.IsAreaBreakBefore( LayoutInfo layoutInfo )
        {
            return false;
        }

        bool IAreaProvider.PositionVertically( LayoutInfo layoutInfo )
        {
            return false;
        }

        bool IAreaProvider.PositionHorizontally( LayoutInfo layoutInfo )
        {
            return false;
        }

        private XUnit CalcContentHeight( DocumentRenderer documentRenderer )
        {
            XUnit height = RenderInfo.GetTotalHeight( GetRenderInfos() );
            if ( height == 0 )
            {
                height = ParagraphRenderer.GetLineHeight( cell.Format, gfx, documentRenderer );
                height += cell.Format.SpaceBefore;
                height += cell.Format.SpaceAfter;
            }
            return height;
        }

        XUnit contentHeight = 0;

        internal RenderInfo[] GetRenderInfos()
        {
            if ( renderInfos != null )
                return renderInfos.ToArray();

            return null;
        }
        RenderInfo lastRenderInfo = null;
        internal void ClearRenderInfos()
        {
            if ( renderInfos == null )
            {
                return;
            }
            //lastRenderInfo = renderInfos != null && renderInfos.Count > 0 ? renderInfos.Last() : null;
            renderInfos.Clear();
        }

        private FieldInfos fieldInfos;
        private List<RenderInfo> renderInfos;
        private XUnit xOffset;
        private XUnit yOffset;
        private Cell cell;
        private TopDownFormatter formatter;
        BordersRenderer bordersRenderer;
        XGraphics gfx;
        DocumentRenderer documentRenderer;
    }
}
