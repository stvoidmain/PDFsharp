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

using System.Linq;
using System.Collections;
using System.Collections.Generic;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.DocumentObjectModel.Visitors;

namespace MigraDoc.Rendering
{
    /// <summary>
    /// Formatting information for tables.
    /// </summary>
    internal class TableFormatInfo : FormatInfo
    {
        internal TableFormatInfo()
        {
        }

        internal override bool EndingIsComplete
        {
            get
            {
                var hasEnded = true;
                if ( formattedCells != null && formattedCells.Count > 0 )
                {
                    //hasEnded = formattedCells.All( fc => fc.Value.Done );
                }
                return this.isEnding && hasEnded;
            }
        }


        internal override bool StartingIsComplete
        {
            get { return !this.IsEmpty && this.startRow > this.lastHeaderRow; }
        }

        internal override bool IsComplete
        {
            get { return false; }
        }

        internal override bool IsEmpty
        {
            get { return this.startRow < 0; }
        }

        internal override bool IsEnding
        {
            get { return this.isEnding; }
        }
        internal bool isEnding;

        internal override bool IsStarting
        {
            get
            {
                return this.startRow == this.lastHeaderRow + 1;
            }
        }

        public override string ToString()
        {
            return string.Format( "start: {0}, end: {1}, cRi: {2}, tRc: {3}", startRow, endRow, cellRenderInfos != null ? cellRenderInfos.Count : 0, formattedCells.First().Key.Table.Rows.Count );
        }

        internal int startColumn = -1;
        internal int endColumn = -1;

        internal int startRow = -1;
        internal int endRow = -1;

        internal int lastHeaderRow = -1;
        internal Dictionary<Cell, FormattedCell> formattedCells;
        internal Dictionary<Cell, IEnumerable<RenderInfo>> cellRenderInfos;
        internal MergedCellList mergedCells;
        internal SortedList bottomBorderMap;
        internal SortedList connectedRowsMap;
        internal int lastRenderedRow = -1;
    }
}
