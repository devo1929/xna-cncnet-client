using System;
using System.Collections.Generic;
using System.Linq;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace ClientGUI;

public class XNAClientListBox : XNAListBox
{
    public XNAClientListBox(WindowManager windowManager) : base(windowManager)
    {
    }

    public void SetItems(IEnumerable<XNAListBoxItem> items)
    {
        Clear();
        foreach (XNAListBoxItem xnaListBoxItem in items ?? Array.Empty<XNAListBoxItem>())
            AddItem(xnaListBoxItem);
    }
    
}