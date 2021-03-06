﻿namespace Mappy.Services
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    using Mappy.Models;
    using Mappy.Views;

    public interface IDialogService
    {
        string AskUserToChooseMap(IList<string> maps);

        string AskUserToOpenFile();

        string AskUserToSaveFile();

        string AskUserToSaveMinimap();

        string AskUserToSaveMapImage();

        string AskUserToChooseMinimap();

        string AskUserToSaveHeightmap();

        string AskUserToChooseHeightmap(int width, int height);

        SectionImportPaths AskUserToChooseSectionImportPaths();

        void CapturePreferences();

        Size AskUserNewMapSize();

        Color? AskUserGridColor(Color previousColor);

        DialogResult AskUserToDiscardChanges();

        MapAttributesResult AskUserForMapAttributes(MapAttributesResult r);

        void ShowError(string message);

        IProgressView CreateProgressView();

        void ShowAbout();
    }
}
