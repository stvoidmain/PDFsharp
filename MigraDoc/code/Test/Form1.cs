﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.IO;
using MigraDoc.Rendering;

namespace Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            var doc = new Document();
            doc.DefaultPageSetup.PageFormat = PageFormat.A4;
            var s = doc.AddSection();
            var t = s.AddTable();
            t.Borders.Width = "1pt";
            t.Borders.Color = Colors.Red;
            var w = doc.DefaultPageSetup.PageWidth - doc.DefaultPageSetup.LeftMargin - doc.DefaultPageSetup.RightMargin;
            t.AddColumn( w );
            t.AddRow();

            var rnd = new Random( 12345 );

            var t2 = t[ 0, 0 ].Elements.AddTable();
            t2.Borders.Width = "0.5pt";
            t2.Borders.Color = Colors.Blue;
            t2.AddColumn( w - 8 );
            for ( int i = 0; i < 30; i++ )
            {
                t2.AddRow();
                t2[ i, 0 ].AddParagraph( "Celda " + i );

                if ( rnd.Next( 2 ) > 0 )
                {
                    t2[ i, 0 ].AddParagraph( "Celda (2) " + i );
                }
            }

            t.AddRow();
            var t3 = t[ 1, 0 ].Elements.AddTable();
            t3.Borders.Width = "0.5pt";
            t3.Borders.Color = Colors.Green;
            t3.AddColumn( w - 8 );
            for ( int i = 0; i < 70; i++ )
            {
                t3.AddRow();
                var text = "Celda2 " + i;
                var p = t3[ i, 0 ].AddParagraph( "Celda2 " + i );
                p.Format.WidowControl = true;
                while ( rnd.Next( 2 ) > 0 )
                {
                    text += Environment.NewLine;
                    text += "Celda2 (2) " + i;
                    //p.AddLineBreak();
                    //p.AddFormattedText( "Celda2 (2) " + i, TextFormat.Bold );
                }
                if ( i == 7 )
                {
                    text += Environment.NewLine;
                    text += " More text to make split, it is going to split or is it not?";// I don't know but i gonna make sure it does!";
                    //text += Environment.NewLine;
                    text += "\nSplitted?";
                }
                if ( i == 34 )
                {
                    //p = t3[ i, 0 ].AddParagraph( "Celda2 (long) " + i );
                    //p.AddText( " A very, very long text to make the cell split the paragraph in it." );
                }
                p.AddText( text.Replace( " ", "­ ­" ) );
            }

            var t4 = t3[ 19, 0 ].Elements.AddTable();
            t4.Borders.Width = "0.5pt";
            t4.Borders.Color = Colors.Yellow;
            t4.AddColumn( ( w - 16 ) / 2 );
            t4.AddColumn( ( w - 16 ) / 2 );
            for ( int i = 0; i < 50; i++ )
            {
                t4.AddRow();
                t4[ i, 0 ].AddParagraph( "Celda3 " + i );
                while ( rnd.Next( 2 ) > 0 )
                {
                    t4[ i, 0 ].AddParagraph( "Celda3 (2)" + i );
                }

                t4[ i, 1 ].AddParagraph( "Celda3 " + i );
                while ( rnd.Next( 2 ) > 0 )
                {
                    t4[ i, 1 ].AddParagraph( "Celda3 (2)" + i );
                }
            }

            var renderer = new PdfDocumentRenderer( true, PdfSharp.Pdf.PdfFontEmbedding.Automatic )
            {
                Document = doc,
                WorkingDirectory = Environment.CurrentDirectory
            };
            renderer.RenderDocument();
            documentPreview1.Document = doc;
            renderer.Save( "test.pdf" );
        }

        private void button1_Click( object sender, EventArgs e )
        {
            if ( documentPreview1.Page < documentPreview1.PageCount )
            {
                documentPreview1.Page = documentPreview1.Page + 1;
            }
        }
    }
}
