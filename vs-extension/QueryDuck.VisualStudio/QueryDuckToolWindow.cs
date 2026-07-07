using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace QueryDuck.VisualStudio;

[Guid(QueryDuckToolWindow.ToolWindowGuidString)]
public sealed class QueryDuckToolWindow : ToolWindowPane
{
    public const string ToolWindowGuidString = "9a4b3c2d-5e6f-7081-92a3-b4c5d6e7f809";

    public QueryDuckToolWindow()
        : base(null)
    {
        Caption = "QueryDuck";
        Content = new QueryDuckToolWindowControl();
    }
}
