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
            this.bordersRenderer = new BordersRenderer( cellBorders, null );
            this.documentRenderer = documentRenderer;

            foreach ( DocumentObject item in cell.Elements )
            {
                if ( item is Table )
                {
                    cellTables.Enqueue( item as Table );
                }
            }
            allTablesProcessed = cellTables.Count == tablesProcesed.Count;

            this.renderInfos = new List<RenderInfo>();
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
                initialRect = CalcContentRect();
            }
        }
        bool isFirstArea = true;
        internal List<ProcessedTable> tablesProcesed = new List<ProcessedTable>();
        Queue<Table> cellTables = new Queue<Table>();
        Dictionary<Table, Area> areas = new Dictionary<Table, Area>();
        XUnit accumulatedheight;
        private Area currentArea;
        private Area firstArea;
        Area IAreaProvider.GetNextArea()
        {
            if ( this.isFirstArea )
            {
                accumulatedheight = 0;
                tablesProcesed = new List<ProcessedTable>();
                areas = new Dictionary<Table, Area>();
                Rectangle rect = CalcContentRect();
                this.isFirstArea = false;
                currentArea = firstArea = rect;
                return rect;
            }
            if ( isReFormat )
            {
                isReFormat = false;// One pass?
                return !Done ? CalcContentRect() : null;
            }
            return null;
        }
        private bool allTablesProcessed = false;

        internal Area GetNestedTableArea( Table t )
        {
            return areas.ContainsKey( t ) ? areas[ t ] : null;
        }

        Area IAreaProvider.ProbeNextArea()
        {
            return null;
        }

        private int lastIndex;

        internal void Format( XGraphics gfx )
        {
            if ( !Done && lastIndex > 0 && lastIndex == cell.Elements.Count )
            {
                lastIndex--;
            }
            if ( lastRenderInfo != null )
            {
                var obByIndex = cell.Elements[ lastIndex ];
                if ( lastRenderInfo.DocumentObject != obByIndex )
                {
                    lastRenderInfo = null;
                }
            }
            this.gfx = gfx;
            this.formatter = new TopDownFormatter( this, this.documentRenderer, this.cell.Elements );
            this.formatter.FormatOnAreas( gfx, false, lastIndex, lastRenderInfo != null ? lastRenderInfo.FormatInfo : null );
            this.contentHeight = CalcContentHeight( this.documentRenderer );
            Done = formatter.LastIndex >= cell.Elements.Count && formatter.LastRenderInfo == null && InnerHeight <= Constrain.Height;
            lastIndex = formatter.LastIndex;
            lastRenderInfo = formatter.LastRenderInfo;
        }

        private bool isReFormat;
        internal void ReFormat( XGraphics gfx )
        {
            if ( Done )
            {
                return;
            }
            isReFormat = true;
            this.renderInfos = new List<RenderInfo>();
            Format( gfx );
            isReFormat = false;
        }

        private Rectangle CalcContentRect()
        {
            Column column = this.cell.Column;
            XUnit width = InnerWidth;
            width -= column.LeftPadding.Point;
            Column rightColumn = this.cell.Table.Columns[ column.Index + this.cell.MergeRight ];
            width -= rightColumn.RightPadding.Point;

            XUnit height = double.MaxValue;// isFirstArea ? Constrain.Height.Point : double.MaxValue;
            return new Rectangle( this.xOffset, this.yOffset, width, Constrain.Height.Point );
        }

        internal XUnit ContentHeight
        {
            get { return this.contentHeight; }
        }

        internal XUnit InnerHeight
        {
            get
            {
                Row row = this.cell.Row;
                XUnit verticalPadding = row.TopPadding.Point;
                verticalPadding += row.BottomPadding.Point;
                switch ( row.HeightRule )
                {
                    case RowHeightRule.Exactly:
                        return row.Height.Point;

                    case RowHeightRule.Auto:
                        return verticalPadding + this.contentHeight;

                    case RowHeightRule.AtLeast:
                    default:
                        //return Math.Max( row.Height, verticalPadding + this.contentHeight );
                        return Math.Max( row.Height, Math.Min( Constrain.Height, verticalPadding + this.contentHeight ) );
                }
            }
        }

        internal XUnit InnerWidth
        {
            get
            {
                XUnit width = 0;
                int cellColumnIdx = this.cell.Column.Index;
                for ( int toRight = 0; toRight <= this.cell.MergeRight; ++toRight )
                {
                    int columnIdx = cellColumnIdx + toRight;
                    width += this.cell.Table.Columns[ columnIdx ].Width;
                }
                width -= this.bordersRenderer.GetWidth( BorderType.Right );

                return width;
            }
        }

        FieldInfos IAreaProvider.AreaFieldInfos
        {
            get
            {
                return this.fieldInfos;
            }
        }

        void IAreaProvider.StoreRenderInfos( IEnumerable<RenderInfo> renderInfos )
        {
            StoreRenderInfos( renderInfos );
        }

        internal void StoreRenderInfos( IEnumerable<RenderInfo> renderInfos, bool overwrite = false )
        {
            if ( renderInfos != null && renderInfos.First() is TableRenderInfo )
            {
                var tri = renderInfos.First() as TableRenderInfo;
                var fri = tri.FormatInfo as TableFormatInfo;
                System.Diagnostics.Debug.WriteLine( "StoreRenderInfos: {0}", fri );
            }
            if ( overwrite )
            {
                this.renderInfos = new List<RenderInfo>( renderInfos );
                //lastRenderInfo = this.renderInfos != null && this.renderInfos.Count > 0 ? this.renderInfos.Last() : null;
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
                height = ParagraphRenderer.GetLineHeight( this.cell.Format, this.gfx, documentRenderer );
                height += this.cell.Format.SpaceBefore;
                height += this.cell.Format.SpaceAfter;
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
            lastRenderInfo = renderInfos != null && renderInfos.Count > 0 ? renderInfos.Last() : null;
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
