using System;
using System.Windows;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.Overrides;

namespace antiGGGravity.Views.Overrides
{
    public partial class TextAuditView : Window
    {
        private readonly ExternalEvent _auditEvent;
        private readonly TextAuditHandler _auditHandler;

        public TextAuditView(ExternalEvent auditEvent, TextAuditHandler auditHandler)
        {
            InitializeComponent();
            _auditEvent = auditEvent;
            _auditHandler = auditHandler;
            _auditHandler.StatusCallback = (msg) => Dispatcher.Invoke(() => UI_Status.Text = msg);
        }

        private void Merge_Click(object sender, RoutedEventArgs e) => RunAction(TextAuditHandler.AuditAction.Merge);
        private void MergeParagraphs_Click(object sender, RoutedEventArgs e) => RunAction(TextAuditHandler.AuditAction.MergeParagraphs);
        private void RemoveReturns_Click(object sender, RoutedEventArgs e) => RunAction(TextAuditHandler.AuditAction.RemoveReturns);
        private void RemoveExtraSpaces_Click(object sender, RoutedEventArgs e) => RunAction(TextAuditHandler.AuditAction.RemoveExtraSpaces);
        private void ToUpper_Click(object sender, RoutedEventArgs e) => RunAction(TextAuditHandler.AuditAction.ToUpper);
        private void ToLower_Click(object sender, RoutedEventArgs e) => RunAction(TextAuditHandler.AuditAction.ToLower);
        private void ToTitle_Click(object sender, RoutedEventArgs e) => RunAction(TextAuditHandler.AuditAction.ToTitle);

        private void RunAction(TextAuditHandler.AuditAction action)
        {
            UI_Status.Text = "Processing...";
            _auditHandler.CurrentAction = action;
            _auditEvent.Raise();
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
