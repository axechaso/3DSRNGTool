using System;
using System.Linq;
using System.Windows.Forms;
using Pk3DSRNGTool.Properties;

namespace Pk3DSRNGTool
{
    public static class FormUtil
    {
        #region Message Displays

        internal static string TranslateMessage(string message) => string.Join(Environment.NewLine,
            (message ?? string.Empty).Replace("\r\n", "\n").Split('\n').Select(Translator.TranslateText));

        /// <summary>
        /// Displays a dialog showing the details of an error.
        /// </summary>
        /// <param name="lines">User-friendly message about the error.</param>
        /// <returns>The <see cref="DialogResult"/> associated with the dialog.</returns>
        internal static DialogResult Error(params string[] lines)
        {
            System.Media.SystemSounds.Exclamation.Play();
            string msg = string.Join(Environment.NewLine + Environment.NewLine, lines.Select(TranslateMessage));
            return MessageBox.Show(msg, Translator.TranslateText("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        internal static DialogResult Alert(params string[] lines)
        {
            System.Media.SystemSounds.Asterisk.Play();
            string msg = string.Join(Environment.NewLine + Environment.NewLine, lines.Select(TranslateMessage));
            return MessageBox.Show(msg, Translator.TranslateText("Alert"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        internal static DialogResult Prompt(MessageBoxButtons btn, params string[] lines)
        {
            System.Media.SystemSounds.Question.Play();
            string msg = string.Join(Environment.NewLine + Environment.NewLine, lines.Select(TranslateMessage));
            return MessageBox.Show(msg, Translator.TranslateText("Prompt"), btn, MessageBoxIcon.Asterisk);
        }
        #endregion

        public static string[] getStringList(string f, string l)
        {
            object txt = Resources.ResourceManager.GetObject("text_" + f + "_" + l); // Fetch File, \n to list.
            if (txt == null) return new string[0];
            string[] rawlist = ((string)txt).Split('\n');
            for (int i = 0; i < rawlist.Length; i++)
                rawlist[i] = rawlist[i].Trim();
            return rawlist;
        }
    }
}
