using Autodesk.AutoCAD.Runtime;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadDevices.DbEditor
{
    public sealed class DbEditorCommands
    {
        [CommandMethod("DB_EDIT")]
        public void EditDatabase()
        {
            using (var editor = new DbEditorForm())
                AcadApplication.ShowModalDialog(editor);
        }
    }
}
