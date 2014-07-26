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
//
// Andrew Tsekhansky (mailto:pakeha07@gmail.com): Table rendering optimization in 2010

#endregion

using System;
using System.Collections;
using System.Linq;
using PdfSharp.Drawing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Visitors;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.DocumentObjectModel.IO;
using System.Collections.Generic;

namespace MigraDoc.Rendering
{
    /// <summary>
    /// Renders a table to an XGraphics object.
    /// </summary>
    internal class TableRenderer : Renderer
    {
        internal TableRenderer( XGraphics gfx, Table documentObject, FieldInfos fieldInfos )
          :
          base( gfx, documentObject, fieldInfos )
        {
            table = documentObject;
        }

        internal TableRenderer( XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos )
          :
          base( gfx, renderInfo, fieldInfos )
        {
            table = ( Table ) this.renderInfo.DocumentObject;
        }

        internal override LayoutInfo InitialLayoutInfo
        {
            get
            {
                LayoutInfo layoutInfo = new LayoutInfo();
                layoutInfo.KeepTogether = table.KeepTogether;
                layoutInfo.KeepWithNext = false;
                layoutInfo.MarginBottom = 0;
                layoutInfo.MarginLeft = 0;
                layoutInfo.MarginTop = 0;
                layoutInfo.MarginRight = 0;
                return layoutInfo;
            }
        }

        private int lastRenderedRow = -1;
        void InitRendering()
        {
            TableFormatInfo formatInfo = ( TableFormatInfo ) renderInfo.FormatInfo;
            bottomBorderMap = formatInfo.bottomBorderMap;
            connectedRowsMap = formatInfo.connectedRowsMap;
            formattedCells = formatInfo.formattedCells;
            cellRenderInfos = formatInfo.cellRenderInfos;

            currRow = formatInfo.startRow;
            startRow = formatInfo.startRow;
            endRow = formatInfo.endRow;
            lastRenderedRow = formatInfo.lastRenderedRow;

            mergedCells = formatInfo.mergedCells;
            lastHeaderRow = formatInfo.lastHeaderRow;
            startX = renderInfo.LayoutInfo.ContentArea.X;
            startY = renderInfo.LayoutInfo.ContentArea.Y;
        }

        /// <summary>
        /// 
        /// </summary>
        void RenderHeaderRows()
        {
            if ( lastHeaderRow < 0 )
                return;

            foreach ( Cell cell in mergedCells.GetCells( 0, lastHeaderRow ) )
                RenderCell( cell );
        }

        void RenderCell( Cell cell )
        {
            Rectangle innerRect = GetInnerRect( CalcStartingHeight(), cell );
            RenderShading( cell, innerRect );
            RenderContent( cell, innerRect );
            RenderBorders( cell, innerRect );
        }

        void RenderShading( Cell cell, Rectangle innerRect )
        {
            ShadingRenderer shadeRenderer = new ShadingRenderer( gfx, cell.Shading );
            shadeRenderer.Render( innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height );
        }

        void RenderBorders( Cell cell, Rectangle innerRect )
        {
            XUnit leftPos = innerRect.X;
            XUnit rightPos = leftPos + innerRect.Width;
            XUnit topPos = innerRect.Y;
            XUnit bottomPos = innerRect.Y + innerRect.Height;
            Borders mergedBorders = mergedCells.GetEffectiveBorders( cell );

            BordersRenderer bordersRenderer = new BordersRenderer( mergedBorders, gfx );
            XUnit bottomWidth = bordersRenderer.GetWidth( BorderType.Bottom );
            XUnit leftWidth = bordersRenderer.GetWidth( BorderType.Left );
            XUnit topWidth = bordersRenderer.GetWidth( BorderType.Top );
            XUnit rightWidth = bordersRenderer.GetWidth( BorderType.Right );

            bordersRenderer.RenderVertically( BorderType.Right, rightPos, topPos, bottomPos + bottomWidth - topPos );
            bordersRenderer.RenderVertically( BorderType.Left, leftPos - leftWidth, topPos, bottomPos + bottomWidth - topPos );
            bordersRenderer.RenderHorizontally( BorderType.Bottom, leftPos - leftWidth, bottomPos, rightPos + rightWidth + leftWidth - leftPos );
            bordersRenderer.RenderHorizontally( BorderType.Top, leftPos - leftWidth, topPos - topWidth, rightPos + rightWidth + leftWidth - leftPos );

            RenderDiagonalBorders( mergedBorders, innerRect );
        }

        void RenderDiagonalBorders( Borders mergedBorders, Rectangle innerRect )
        {
            BordersRenderer bordersRenderer = new BordersRenderer( mergedBorders, gfx );
            bordersRenderer.RenderDiagonally( BorderType.DiagonalDown, innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height );
            bordersRenderer.RenderDiagonally( BorderType.DiagonalUp, innerRect.X, innerRect.Y, innerRect.Width, innerRect.Height );
        }

        void RenderContent( Cell cell, Rectangle innerRect )
        {
            FormattedCell formattedCell = formattedCells[ cell ];
            RenderInfo[] renderInfos = cellRenderInfos[ cell ].ToArray();//formattedCell.GetRenderInfos();//

            if ( renderInfos == null )
                return;

            VerticalAlignment verticalAlignment = cell.VerticalAlignment;
            XUnit contentHeight = formattedCell.ContentHeight;
            XUnit innerHeight = innerRect.Height;
            XUnit targetX = innerRect.X + cell.Column.LeftPadding;

            XUnit targetY;
            if ( verticalAlignment == VerticalAlignment.Bottom )
            {
                targetY = innerRect.Y + innerRect.Height;
                targetY -= cell.Row.BottomPadding;
                targetY -= contentHeight;
            }
            else if ( verticalAlignment == VerticalAlignment.Center )
            {
                targetY = innerRect.Y + cell.Row.TopPadding;
                targetY += innerRect.Y + innerRect.Height - cell.Row.BottomPadding;
                targetY -= contentHeight;
                targetY /= 2;
            }
            else
                targetY = innerRect.Y + cell.Row.TopPadding;

            var lri = renderInfos.ToList();
            if ( lri.Any( r => r is TableRenderInfo ) )
            {
                foreach ( var tri in lri.Where( r => r is TableRenderInfo ).Cast<TableRenderInfo>() )
                {
                    var fri = tri.FormatInfo as TableFormatInfo;
                    System.Diagnostics.Debug.WriteLine( "RenderInfo (TableRenderer): {0}", fri );
                }
            }
            //renderInfos = lri.ToArray();
            RenderByInfos( targetX, targetY, renderInfos );
        }



        Rectangle GetInnerRect( XUnit startingHeight, Cell cell )
        {
            BordersRenderer bordersRenderer = new BordersRenderer( mergedCells.GetEffectiveBorders( cell ), gfx );
            FormattedCell formattedCell = formattedCells[ cell ];
            XUnit width = formattedCell.InnerWidth;

            XUnit y = startY;
            if ( cell.Row.Index > lastHeaderRow )
                y += startingHeight;
            else
                y += CalcMaxTopBorderWidth( 0 );

            XUnit upperBorderPos = ( XUnit ) bottomBorderMap[ cell.Row.Index ];

            y += upperBorderPos;
            if ( cell.Row.Index > lastHeaderRow )
                y -= ( XUnit ) bottomBorderMap[ startRow ];

            XUnit lowerBorderPos = ( XUnit ) bottomBorderMap[ cell.Row.Index + cell.MergeDown + 1 ];


            XUnit height = lowerBorderPos - upperBorderPos;
            height -= bordersRenderer.GetWidth( BorderType.Bottom );

            XUnit x = startX;
            for ( int clmIdx = 0; clmIdx < cell.Column.Index; ++clmIdx )
            {
                x += table.Columns[ clmIdx ].Width;
            }
            x += LeftBorderOffset;

            return new Rectangle( x, y, width, height );
        }

        internal override void Render()
        {
            InitRendering();
            if ( lastRenderedRow >= endRow )
            {
                //return;
            }
            RenderHeaderRows();
            if ( startRow <= endRow )
            {
                for ( int i = startRow; i <= endRow; i++ )
                {
                    foreach ( Cell cell in mergedCells.GetRowCells( i ) )
                        RenderCell( cell );

                    ( ( TableFormatInfo ) renderInfo.FormatInfo ).lastRenderedRow = lastRenderedRow = i;
                }
            }
        }

        void InitFormat( Area area, FormatInfo previousFormatInfo )
        {
            TableFormatInfo prevTableFormatInfo = ( TableFormatInfo ) previousFormatInfo;
            TableRenderInfo tblRenderInfo = new TableRenderInfo();
            tblRenderInfo.table = table;

            renderInfo = tblRenderInfo;
            cellRenderInfos = new Dictionary<Cell, IEnumerable<RenderInfo>>();

            if ( prevTableFormatInfo != null )
            {
                mergedCells = prevTableFormatInfo.mergedCells;
                formattedCells = prevTableFormatInfo.formattedCells;
                startRow = prevTableFormatInfo.endRow + 1;
                if ( formattedCells.Any( fc => fc.Key.Row.Index < startRow && !fc.Value.Done ) )
                {
                    startRow--;
                }
                FormatCells( area, true );
                processedTables = prevTableFormatInfo.processedTables;
                //cellRenderInfos = prevTableFormatInfo.cellRenderInfos;
                bottomBorderMap = prevTableFormatInfo.bottomBorderMap;
                lastHeaderRow = prevTableFormatInfo.lastHeaderRow;
                connectedRowsMap = prevTableFormatInfo.connectedRowsMap;
            }
            else
            {
                //cellRenderInfos = new Dictionary<Cell, IEnumerable<RenderInfo>>();
                mergedCells = new MergedCellList( table );
                FormatCells( area );
                ProcessTableCells();
                CalcLastHeaderRow();
                CreateConnectedRows();
                CreateBottomBorderMap();
                if ( doHorizontalBreak )
                {
                    CalcLastHeaderColumn();
                    CreateConnectedColumns();
                }
                startRow = lastHeaderRow + 1;
            }
            ( ( TableFormatInfo ) tblRenderInfo.FormatInfo ).mergedCells = mergedCells;
            ( ( TableFormatInfo ) tblRenderInfo.FormatInfo ).formattedCells = formattedCells;
            ( ( TableFormatInfo ) tblRenderInfo.FormatInfo ).processedTables = processedTables;
            ( ( TableFormatInfo ) tblRenderInfo.FormatInfo ).cellRenderInfos = cellRenderInfos;
            ( ( TableFormatInfo ) tblRenderInfo.FormatInfo ).bottomBorderMap = bottomBorderMap;
            ( ( TableFormatInfo ) tblRenderInfo.FormatInfo ).connectedRowsMap = connectedRowsMap;
            ( ( TableFormatInfo ) tblRenderInfo.FormatInfo ).lastHeaderRow = lastHeaderRow;
        }

        void FormatCells( Area constrain, bool reFormat = false )
        {
            if ( reFormat )
            {
                ReFormatCells( constrain );
                return;
            }
            formattedCells = new Dictionary<Cell, FormattedCell>();
            foreach ( Cell cell in mergedCells.GetCells() )
            {
                FormattedCell formattedCell = new FormattedCell( cell, documentRenderer, mergedCells.GetEffectiveBorders( cell ), fieldInfos, 0, 0 )
                {
                    Constrain = constrain
                };
                formattedCell.Format( gfx );
                formattedCells.Add( cell, formattedCell );
            }
        }

        void ReFormatCells( Area constrain )
        {
            foreach ( Cell cell in mergedCells.GetCells() )
            {
                if ( formattedCells.ContainsKey( cell ) && !formattedCells[ cell ].Done )
                {
                    formattedCells[ cell ].Constrain = constrain;
                    formattedCells[ cell ].ReFormat( gfx );
                }
            }
            CreateBottomBorderMap();
        }

        private void ProcessTableCells()
        {
            processedTables = new Dictionary<Cell, ProcessedTable>();
            for ( int i = 0; i < table.Rows.Count; i++ )
            {
                foreach ( Cell rowCell in mergedCells.GetRowCells( i ) )
                {
                    if ( rowCell.Elements != null )
                    {
                        for ( int j = 0; j < rowCell.Elements.Count; j++ )
                        {
                            if ( rowCell.Elements[ j ] is Table )
                            {
                                var t = rowCell.Elements[ j ] as Table;
                                processedTables.Add( rowCell, new ProcessedTable( t ) );
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Formats (measures) the table.
        /// </summary>
        /// <param name="area">The area on which to fit the table.</param>
        /// <param name="previousFormatInfo"></param>
        internal override void Format( Area area, FormatInfo previousFormatInfo )
        {
            DocumentElements elements = DocumentRelations.GetParent( table ) as DocumentElements;
            if ( elements != null )
            {
                Section section = DocumentRelations.GetParent( elements ) as Section;
                if ( section != null )
                    doHorizontalBreak = section.PageSetup.HorizontalPageBreak;
            }

            renderInfo = new TableRenderInfo();
            InitFormat( area, previousFormatInfo );

            // Don't take any Rows higher then MaxElementHeight
            XUnit topHeight = CalcStartingHeight();
            XUnit probeHeight = topHeight;
            XUnit offset = 0;
            if ( startRow > lastHeaderRow + 1 &&
              startRow < table.Rows.Count )
                offset = ( XUnit ) bottomBorderMap[ startRow ] - topHeight;
            else
                offset = -CalcMaxTopBorderWidth( 0 );

            int probeRow = startRow;
            XUnit currentHeight = 0;
            XUnit startingHeight = 0;
            bool isEmpty = false;

            while ( probeRow < table.Rows.Count )
            {
                bool firstProbe = probeRow == startRow;
                probeRow = ( int ) connectedRowsMap[ probeRow ];
                var anyTableNotDone = mergedCells.GetRowCells( probeRow ).Any( c => processedTables.ContainsKey( c ) && !processedTables[ c ].Done );
                // Don't take any Rows higher then MaxElementHeight
                probeHeight = ( XUnit ) bottomBorderMap[ probeRow + 1 ] - offset;
                if ( firstProbe && probeHeight > MaxElementHeight - Tolerance )
                    probeHeight = MaxElementHeight - Tolerance;
                if ( anyTableNotDone && previousFormatInfo != null )
                {
                    probeHeight = 0;
                    for ( int i = startRow; i <= probeRow; i++ )
                    {
                        probeHeight += formattedCells.Where( fc => fc.Key.Row.Index == i ).Max( fc => fc.Value.InnerHeight.Point );
                    }
                }

                //The height for the first new row(s) + headerrows:
                if ( startingHeight == 0 )
                {
                    if ( probeHeight > area.Height && !anyTableNotDone )
                    {
                        isEmpty = true;
                        break;
                    }

                    startingHeight = probeHeight;
                }
                //if ( anyTableNotDone )
                //{
                //    var pt = processedTables.FirstOrDefault( p => p.Key == mergedCells.GetRowCells( probeRow ).First() && !p.Value.Done ).Value;
                //    var tRenderer = Create( gfx, documentRenderer, pt.table, null ) as TableRenderer;
                //    tRenderer.Format( new Rectangle( 0, 0, area.Width, double.MaxValue ), null );
                //    if ( pt.lastRow > 0 )
                //    {
                //        XUnit h = 0;
                //        for ( int innerTableRow = pt.lastRow; innerTableRow < pt.table.Rows.Count; innerTableRow++ )
                //        {
                //            var cells = tRenderer.formattedCells.Where( c => c.Key.Row.Index == innerTableRow ).Select( c => c.Value ).ToList();
                //            var maxCell = cells.Max( c => c.ContentHeight.Point );
                //            h += maxCell;
                //        }
                //        probeHeight = Math.Max( probeHeight, h );
                //    }
                //    else
                //    {
                //        probeHeight = Math.Max( probeHeight, tRenderer.RenderInfo.LayoutInfo.ContentArea.Height );
                //    }
                //}
                if ( probeHeight > area.Height /*|| anyTableNotDone */)
                {
                    if ( !anyTableNotDone && previousFormatInfo != null )
                    {
                        XUnit maxRowHeight = formattedCells.Where( fc => fc.Key.Row.Index == probeRow ).Max( fc => fc.Value.InnerHeight.Point );
                        if ( maxRowHeight < probeHeight && maxRowHeight < area.Height )
                        {
                            currRow = probeRow;
                            currentHeight = maxRowHeight;
                            ++probeRow;
                            continue;
                        }
                    }
                    break;
                    //var tableFound = false;
                    //var tableFits = false;
                    //foreach ( Cell rowCell in mergedCells.GetRowCells( probeRow ) )
                    //{
                    //    if ( processedTables.ContainsKey( rowCell ) )
                    //    {
                    //        var pt = processedTables[ rowCell ];
                    //        if ( !pt.Done )
                    //        {
                    //            tableFound = true;
                    //            var tRenderer = Create( gfx, documentRenderer, pt.table, null ) as TableRenderer;
                    //            tRenderer.Format( new Rectangle( 0, 0, area.Width, double.MaxValue ), null );
                    //            var tHeight = tRenderer.RenderInfo.LayoutInfo.ContentArea.Height;
                    //            if ( pt.lastRow == 0 && tHeight <= area.Height )
                    //            {
                    //                currentHeight = tHeight;
                    //                pt.lastRow = pt.table.Rows.Count;
                    //                processedTables[ rowCell ] = pt;
                    //                tableFits = true;
                    //                break;
                    //            }
                    //            else
                    //            {
                    //                currentHeight = 0;
                    //                for ( int innerTableRow = pt.lastRow; innerTableRow < pt.table.Rows.Count; innerTableRow++ )
                    //                {
                    //                    var cells = tRenderer.formattedCells.Where( c => c.Key.Row.Index == innerTableRow ).Select( c => c.Value ).ToList();
                    //                    var maxCell = cells.Max( c => c.ContentHeight.Point );
                    //                    if ( currentHeight + maxCell <= area.Height )
                    //                        currentHeight += maxCell;
                    //                    else
                    //                    {
                    //                        pt.lastRow--;
                    //                        break;
                    //                    }

                    //                    pt.lastRow = innerTableRow;
                    //                }

                    //                tableFits = pt.Done;
                    //                processedTables[ rowCell ] = pt;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //    if ( tableFound )
                    //        break;
                    //}
                    //if ( !tableFound || !tableFits )
                    //    break;
                    //else if ( tableFits )
                    //    currRow = probeRow++;
                    //else
                    //    break;
                }
                else
                {
                    if ( anyTableNotDone )
                    {
                        //Obviamente esta/s tabla/s entra/n
                        foreach ( Cell rowCell in mergedCells.GetRowCells( probeRow ) )
                        {
                            if ( processedTables.ContainsKey( rowCell ) )
                            {
                                var pt = processedTables[ rowCell ];
                                var tRenderer = Create( gfx, documentRenderer, pt.table, null ) as TableRenderer;
                                tRenderer.Format( new Rectangle( 0, 0, area.Width, probeHeight ), null );
                                var tHeight = tRenderer.RenderInfo.LayoutInfo.ContentArea.Height;
                                probeHeight = Math.Min( tHeight, probeHeight );
                                processedTables[ rowCell ].lastRow = processedTables[ rowCell ].table.Rows.Count;
                            }
                        }
                    }
                    currRow = probeRow;
                    currentHeight = probeHeight;
                    ++probeRow;
                }
            }

            if ( !isEmpty )
            {
                TableFormatInfo formatInfo = ( TableFormatInfo ) renderInfo.FormatInfo;
                formatInfo.processedTables = processedTables;
                formatInfo.startRow = startRow;
                formatInfo.isEnding = ( currRow >= table.Rows.Count - 1 );// && ( processedTables == null || ( processedTables.Count == 0 || processedTables.All( pt => pt.Value.Done ) ) );
                formatInfo.endRow = currRow;
                if ( formatInfo.isEnding && formattedCells.Any( fc => !fc.Value.Done ) )
                {
                    formatInfo.isEnding = false;
                }
            }
            FinishLayoutInfo( area, currentHeight, startingHeight );
        }

        void FinishLayoutInfo( Area area, XUnit currentHeight, XUnit startingHeight )
        {
            TableFormatInfo formatInfo = ( TableFormatInfo ) renderInfo.FormatInfo;
            LayoutInfo layoutInfo = renderInfo.LayoutInfo;
            layoutInfo.StartingHeight = startingHeight;
            //REM: Trailing height would have to be calculated in case tables had a keep with next property.
            layoutInfo.TrailingHeight = 0;
            if ( currRow >= 0 )
            {
                layoutInfo.ContentArea = new Rectangle( area.X, area.Y, 0, currentHeight );
                XUnit width = LeftBorderOffset;
                foreach ( Column clm in table.Columns )
                {
                    width += clm.Width;
                }
                layoutInfo.ContentArea.Width = width;
            }
            layoutInfo.MinWidth = layoutInfo.ContentArea.Width;
            //var oldInfos = ( ( TableFormatInfo ) renderInfo.FormatInfo ).cellRenderInfos;
            //( ( TableFormatInfo ) renderInfo.FormatInfo ).cellRenderInfos = new Dictionary<Cell, IEnumerable<RenderInfo>>();
            //XUnit acu = 0;
            foreach ( var cell in formattedCells.Where( fc => fc.Key.Row.Index >= formatInfo.startRow && fc.Key.Row.Index <= formatInfo.endRow ) )
            {
                //var cellInfos = cell.Value.GetRenderInfos();
                ( ( TableFormatInfo ) renderInfo.FormatInfo ).cellRenderInfos[ cell.Key ] = cell.Value.GetRenderInfos();
                continue;
                //var total = cellInfos.Sum( r => r.LayoutInfo.ContentArea.Height.Point );

                //if ( !oldInfos.ContainsKey( cell.Key ) )
                //{
                //    if ( acu <= area.Height )
                //    {
                //        ( ( TableFormatInfo ) renderInfo.FormatInfo ).cellRenderInfos[ cell.Key ] = cellInfos;
                //    }
                //    else
                //    {
                //        var rInfos = new List<RenderInfo>();
                //        foreach ( var ri in cellInfos )
                //        {
                //            if ( acu + rInfos.Sum( r => r.LayoutInfo.ContentArea.Height.Point ) + ri.LayoutInfo.ContentArea.Height.Point <= area.Height )
                //            {
                //                rInfos.Add( ri );
                //            }
                //        }
                //        ( ( TableFormatInfo ) renderInfo.FormatInfo ).cellRenderInfos[ cell.Key ] = rInfos.ToList();
                //    }
                //}
                //else
                //{
                //    var existing = oldInfos[ cell.Key ].ToList();
                //    total += existing.Sum( r => r.LayoutInfo.ContentArea.Height.Point );
                //    foreach ( var ri in cellInfos )
                //    {
                //        if ( !existing.Contains( ri ) )
                //        {
                //            if ( total + ri.LayoutInfo.ContentArea.Height.Point < area.Height )
                //                existing.Add( ri );
                //        }
                //    }
                //    ( ( TableFormatInfo ) renderInfo.FormatInfo ).cellRenderInfos[ cell.Key ] = existing.ToList();
                //}
                //oldInfos = ( ( TableFormatInfo ) renderInfo.FormatInfo ).cellRenderInfos;
                //formattedCells[ cell.Key ].ClearRenderInfos();
                //acu += cellInfos.Sum( c => c.LayoutInfo.ContentArea.Height.Point );
            }

            if ( !table.Rows.LeftIndent.IsEmpty )
                layoutInfo.Left = table.Rows.LeftIndent.Point;

            else if ( table.Rows.Alignment == RowAlignment.Left )
            {
                if ( table.Columns.Count > 0 ) // Errors in Wiki syntax can lead to tables w/o columns ...
                {
                    XUnit leftOffset = LeftBorderOffset;
                    leftOffset += table.Columns[ 0 ].LeftPadding;
                    layoutInfo.Left = -leftOffset;
                }
#if DEBUG
                else
                    table.GetType();
#endif
            }

            switch ( table.Rows.Alignment )
            {
                case RowAlignment.Left:
                    layoutInfo.HorizontalAlignment = ElementAlignment.Near;
                    break;

                case RowAlignment.Right:
                    layoutInfo.HorizontalAlignment = ElementAlignment.Far;
                    break;

                case RowAlignment.Center:
                    layoutInfo.HorizontalAlignment = ElementAlignment.Center;
                    break;
            }
        }

        XUnit LeftBorderOffset
        {
            get
            {
                if ( leftBorderOffset < 0 )
                {
                    if ( table.Rows.Count > 0 && table.Columns.Count > 0 )
                    {
                        Borders borders = mergedCells.GetEffectiveBorders( table[ 0, 0 ] );
                        BordersRenderer bordersRenderer = new BordersRenderer( borders, gfx );
                        leftBorderOffset = bordersRenderer.GetWidth( BorderType.Left );
                    }
                    else
                        leftBorderOffset = 0;
                }
                return leftBorderOffset;
            }
        }
        private XUnit leftBorderOffset = -1;

        /// <summary>
        /// Calcs either the height of the header rows or the height of the uppermost top border.
        /// </summary>
        /// <returns></returns>
        XUnit CalcStartingHeight()
        {
            XUnit height = 0;
            if ( lastHeaderRow >= 0 )
            {
                height = ( XUnit ) bottomBorderMap[ lastHeaderRow + 1 ];
                height += CalcMaxTopBorderWidth( 0 );
            }
            else
            {
                if ( table.Rows.Count > startRow )
                    height = CalcMaxTopBorderWidth( startRow );
            }

            return height;
        }


        void CalcLastHeaderColumn()
        {
            lastHeaderColumn = -1;
            foreach ( Column clm in table.Columns )
            {
                if ( clm.HeadingFormat )
                    lastHeaderColumn = clm.Index;
                else break;
            }
            if ( lastHeaderColumn >= 0 )
                lastHeaderRow = CalcLastConnectedColumn( lastHeaderColumn );

            //Ignore heading format if all the table is heading:
            if ( lastHeaderRow == table.Rows.Count - 1 )
                lastHeaderRow = -1;

        }

        void CalcLastHeaderRow()
        {
            lastHeaderRow = -1;
            foreach ( Row row in table.Rows )
            {
                if ( row.HeadingFormat )
                    lastHeaderRow = row.Index;
                else break;
            }
            if ( lastHeaderRow >= 0 )
                lastHeaderRow = CalcLastConnectedRow( lastHeaderRow );

            //Ignore heading format if all the table is heading:
            if ( lastHeaderRow == table.Rows.Count - 1 )
                lastHeaderRow = -1;

        }

        void CreateConnectedRows()
        {
            connectedRowsMap = new SortedList();
            foreach ( Cell cell in mergedCells.GetCells() )
            {
                if ( !connectedRowsMap.ContainsKey( cell.Row.Index ) )
                {
                    int lastConnectedRow = CalcLastConnectedRow( cell.Row.Index );
                    connectedRowsMap[ cell.Row.Index ] = lastConnectedRow;
                }
            }
        }

        void CreateConnectedColumns()
        {
            connectedColumnsMap = new SortedList();
            foreach ( Cell cell in mergedCells.GetCells() )
            {
                if ( !connectedColumnsMap.ContainsKey( cell.Column.Index ) )
                {
                    int lastConnectedColumn = CalcLastConnectedColumn( cell.Column.Index );
                    connectedColumnsMap[ cell.Column.Index ] = lastConnectedColumn;
                }
            }
        }

        void CreateBottomBorderMap()
        {
            bottomBorderMap = new SortedList();
            bottomBorderMap.Add( 0, XUnit.FromPoint( 0 ) );
            while ( !bottomBorderMap.ContainsKey( table.Rows.Count ) )
            {
                CreateNextBottomBorderPosition();
            }
        }

        /// <summary>
        /// Calculates the top border width for the first row that is rendered or formatted.
        /// </summary>
        /// <param name="row">The row index.</param>
        XUnit CalcMaxTopBorderWidth( int row )
        {
            XUnit maxWidth = 0;
            if ( table.Rows.Count > row )
            {
                foreach ( Cell rowCell in mergedCells.GetRowCells( row ) )
                {
                    if ( rowCell.HasBorders )
                    {
                        BordersRenderer bordersRenderer = new BordersRenderer( rowCell.Borders, gfx );
                        maxWidth = Math.Max( maxWidth, bordersRenderer.GetWidth( BorderType.Top ) );
                    }
                }
            }
            return maxWidth;
        }

        /// <summary>
        /// Creates the next bottom border position.
        /// </summary>
        void CreateNextBottomBorderPosition()
        {
            int lastIdx = bottomBorderMap.Count - 1;
            int lastBorderRow = ( int ) bottomBorderMap.GetKey( lastIdx );
            XUnit lastPos = ( XUnit ) bottomBorderMap.GetByIndex( lastIdx );
            Cell minMergedCell = GetMinMergedCell( lastBorderRow );
            FormattedCell minMergedFormattedCell = formattedCells[ minMergedCell ];
            XUnit maxBottomBorderPosition = lastPos + minMergedFormattedCell.InnerHeight;
            maxBottomBorderPosition += CalcBottomBorderWidth( minMergedCell );

            foreach ( Cell cell in mergedCells.GetCells( lastBorderRow, minMergedCell.Row.Index + minMergedCell.MergeDown ) )
                if ( cell.Row.Index + cell.MergeDown == minMergedCell.Row.Index + minMergedCell.MergeDown )
                {
                    FormattedCell formattedCell = formattedCells[ cell ];
                    XUnit topBorderPos = ( XUnit ) bottomBorderMap[ cell.Row.Index ];
                    XUnit bottomBorderPos = topBorderPos + formattedCell.InnerHeight;
                    bottomBorderPos += CalcBottomBorderWidth( cell );
                    maxBottomBorderPosition = Math.Max( maxBottomBorderPosition, bottomBorderPos );
                    //if ( bottomBorderPos > maxBottomBorderPosition )
                    //    maxBottomBorderPosition = bottomBorderPos;
                }

            bottomBorderMap.Add( minMergedCell.Row.Index + minMergedCell.MergeDown + 1, maxBottomBorderPosition );
        }

        /// <summary>
        /// Calculates bottom border width of a cell.
        /// </summary>
        /// <param name="cell">The cell the bottom border of the row that is probed.</param>
        /// <returns>The calculated border width.</returns>
        XUnit CalcBottomBorderWidth( Cell cell )
        {
            Borders borders = mergedCells.GetEffectiveBorders( cell );
            if ( borders != null )
            {
                BordersRenderer bordersRenderer = new BordersRenderer( borders, gfx );
                return bordersRenderer.GetWidth( BorderType.Bottom );
            }
            return 0;
        }

        /// <summary>
        /// Gets the first cell in the given row that is merged down minimally.
        /// </summary>
        /// <param name="row">The row to prope.</param>
        /// <returns>The first cell with minimal vertical merge.</returns>
        Cell GetMinMergedCell( int row )
        {
            int minMerge = table.Rows.Count;
            Cell minCell = null;
            foreach ( Cell cell in mergedCells.GetRowCells( row ) )
            {
                if ( cell.MergeDown == 0 )
                {
                    minCell = cell;
                    break;
                }
                else if ( cell.MergeDown < minMerge )
                {
                    minMerge = cell.MergeDown;
                    minCell = cell;
                }
            }

            return minCell;
        }


        /// <summary>
        /// Calculates the last row that is connected with the given row.
        /// </summary>
        /// <param name="row">The row that is probed for downward connection.</param>
        /// <returns>The last row that is connected with the given row.</returns>
        int CalcLastConnectedRow( int row )
        {
            return mergedCells.CalcLastConnectedRow( row );
            // AndrewT: moved to MergedCells
            //int lastConnectedRow = row;
            //foreach (Cell cell in this.mergedCells)
            //{
            //  if (cell.Row.Index <= lastConnectedRow)
            //  {
            //    int downConnection = Math.Max(cell.Row.KeepWith, cell.MergeDown);
            //    if (lastConnectedRow < cell.Row.Index + downConnection)
            //      lastConnectedRow = cell.Row.Index + downConnection;
            //  }
            //}

            //return lastConnectedRow;
        }

        /// <summary>
        /// Calculates the last column that is connected with the specified column.
        /// </summary>
        /// <param name="column">The column that is probed for downward connection.</param>
        /// <returns>The last column that is connected with the given column.</returns>
        int CalcLastConnectedColumn( int column )
        {
            return mergedCells.CalcLastConnectedColumn( column );
            // AndrewT: moved to MergedCells
            //int lastConnectedColumn = column;
            //foreach (Cell cell in this.mergedCells)
            //{
            //  if (cell.Column.Index <= lastConnectedColumn)
            //  {
            //    int rightConnection = Math.Max(cell.Column.KeepWith, cell.MergeRight);
            //    if (lastConnectedColumn < cell.Column.Index + rightConnection)
            //      lastConnectedColumn = cell.Column.Index + rightConnection;
            //  }
            //}
            //return lastConnectedColumn;
        }



        Table table;
        MergedCellList mergedCells;
        internal Dictionary<Cell, FormattedCell> formattedCells;
        internal Dictionary<Cell, ProcessedTable> processedTables;
        internal Dictionary<Cell, IEnumerable<RenderInfo>> cellRenderInfos;
        SortedList bottomBorderMap;
        SortedList connectedRowsMap;
        SortedList connectedColumnsMap;

        int lastHeaderRow;
        int lastHeaderColumn;
        int startRow;
        int currRow;
        int endRow = -1;

        bool doHorizontalBreak = false;
        XUnit startX;
        XUnit startY;

    }
}
