﻿using System;
using System.IO;
using System.Collections;
using System.Windows.Forms;
using System.Runtime.Serialization.Formatters.Binary;
using MyJobs.Properties;

namespace MyJobs
{
    public partial class FormMain : Form
    {
        /* Constructors */
        public FormMain()
        {
            InitializeComponent();
            Text = Version.AppNameFull;
            Icon = Resources.MyJobs_MainIcon_16;

            trayIcon.Icon = Resources.MyJobs_MainIcon_16;
            trayIcon.Text = Version.AppNameFull;
        }

        /* Events' handlers */
        private void FormLoad(Object sender, EventArgs e)
        {
            // Loading settings from configuration file
            config = Config.LoadConfig();

            // Splashscreen
            FormSplashscreen splash = new FormSplashscreen();

            if (config.AppShowSplashScreen)
            {
                this.Hide();
                splash.Show();
                splash.Status = "Loading...";
            }

            // Main window properties
            Width = config.WindowWidth;
            Height = config.WindowHeight;
            Left = config.WindowLeft;
            Top = config.WindowTop;
            WindowState = config.WindowState;
            menuActionEditShowToolbar.Checked = config.WindowShowToolbar;
            ActionShowToolbar(this, null);

            // Loading database file
            try
            {
                FileStream f = new FileStream(config.DatabaseFileName, FileMode.Open);
                BinaryFormatter ser = new BinaryFormatter();
                listJobs = (ArrayList)ser.Deserialize(f);
                f.Close();
            }
            catch (Exception ex)
            {
                Log.AppendMessage(ex.Message);
                listJobs = new ArrayList();
                jobsChanged = true;
            }

            listJobs.Sort();

            foreach (Job t in listJobs)
            {
                // Looking for expired jobs
                if (!t.Termless && (t.Status != JobStatus.Completed)
                    && (t.DeadlineDate <= DateTime.Now.AddDays(-1))) // yesterday and before
                {
                    t.Status = JobStatus.Expired;
                }

                // Output data to the main window
                AddItemInList(t);
            }

            // End of processing
            statusLJobs.Text = GetNumberOfJobs(JobStatus.Active).ToString() + "/" + listJobs.Count;
            splash.Close();
            this.Show();
        }

        private void ActionAutosave(Object sender, EventArgs e)
        {
            if (jobsChanged)
            {
                try
                {
                    FileStream f = new FileStream(config.DatabaseFileName, FileMode.Create);
                    BinaryFormatter ser = new BinaryFormatter();
                    ser.Serialize(f, listJobs);
                    f.Close();
                    jobsChanged = false;
                }
                catch (Exception ex)
                {
                    Log.AppendMessage(ex.Message);
                    MessageBox.Show(this, ex.Message, "Saving error", MessageBoxButtons.OK);
                }
            }
        }

        // Menu File
        private void ActionClosing(Object sender, FormClosingEventArgs e)
        {
            // Works only when close button on window decoration were pressed
            if ((e.CloseReason == CloseReason.UserClosing)
                && config.AppHideToTrayOnClose)
            {
                ActionShowHide(this, null);
                e.Cancel = true;
            }
        }

        private void ActionExit(Object sender, EventArgs e)
        {
            // Saving main window state
            config.WindowState = WindowState;
            config.WindowWidth = Width;
            config.WindowHeight = Height;
            config.WindowLeft = Left;
            config.WindowTop = Top;
            config.SaveConfig();

            // Saving database
            ActionAutosave(this, null);

            // And exit
            Application.Exit();
        }

        // Menu edit
        private void ActionShowToolbar(Object sender, EventArgs e)
        {
            if ((config.WindowShowToolbar = menuActionEditShowToolbar.Checked) == true)
            {
                toolsMain.Show();
            }
            else
            {
                toolsMain.Hide();
            }
        }

        private void ActionPreferences(Object sender, EventArgs e)
        {
            FormPreferences form = new FormPreferences(ref config);
            form.ShowDialog(this);
        }

        private void ActionStatistic(Object sender, EventArgs e)
        {
            FormStatistic form = new FormStatistic(listJobs.Count,
                                                   GetNumberOfJobs(JobStatus.Completed),
                                                   GetNumberOfJobs(JobStatus.Expired));
            form.ShowDialog(this);
        }

        // Menu job
        private void ActionCreateJob(Object sender, EventArgs e)
        {
            FormCreateJob form = new FormCreateJob();
            form.OnJobAddition += new FormCreateJob.JobAdditionHandler(ActionJobAdding);
            form.ShowDialog(this);
        }

        private void ActionJobAdding(Object sender, Job newJob)
        {
            AddItemInList(newJob);
            listJobs.Add(newJob);
            jobsChanged = true;
        }

        private void ActionEditJob(Object sender, EventArgs e)
        {
            if (listMain.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Nothing to edit! Select job first.", "Error");
                return;
            }

            if (listMain.SelectedItems[0].Text == JobStatus.Completed.ToString())
            {
                MessageBox.Show(this, "Can't edit already completed job.", "Error");
                return;
            }

            FormCreateJob form = new FormCreateJob(GetJobByKey((Int32)listMain.SelectedItems[0].Tag));
            form.OnJobAddition += new FormCreateJob.JobAdditionHandler(AtionJobEditing);
            form.ShowDialog(this);
        }

        private void AtionJobEditing(Object sender, Job editedJob)
        {
            foreach (ListViewItem t in listMain.Items)
            {
                if (editedJob.Key == (Int32)t.Tag)
                {
                    t.SubItems[1].Text = editedJob.Title;

                    if (editedJob.Termless)
                    {
                        t.SubItems[2].Text = "Termless";
                    }
                    else
                    {
                        t.SubItems[2].Text = editedJob.DeadlineDate.ToShortDateString();
                    }

                    break;
                }
            }

            for(Int32 i = 0; i < listJobs.Count;i++)
            {
                if (((Job)listJobs[i]).Key == editedJob.Key)
                {
                    listJobs[i] = editedJob;
                    break;
                }
            }

            jobsChanged = true;
        }

        private void ActionRemoveJob(Object sender, EventArgs e)
        {
            if (listMain.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Nothing to delete! Select item(s) first.", "Error");
                return;
            }

            DialogResult res = MessageBox.Show(this,
                "Do you realy want to delete selected job(s)?",                    // text
                "Removing jobs (" + listMain.SelectedItems.Count.ToString() + ")", // caption
                MessageBoxButtons.YesNo);

            if (res == DialogResult.Yes)
            {
                jobsChanged = true;

                foreach (ListViewItem t in listMain.SelectedItems)
                {
                    RemoveJobByKey((Int32)t.Tag);
                    listMain.Items.Remove(t);
                }
            }
        }

        private void ActionCompleteJob(Object sender, EventArgs e)
        {
            if (listMain.SelectedItems[0].Text == JobStatus.Completed.ToString())
            {
                return;
            }

            // Change job status on the screen
            listMain.SelectedItems[0].Text = JobStatus.Completed.ToString();

            // In the database
            foreach (Job t in listJobs)
            {
                if (t.Key == (Int32)listMain.SelectedItems[0].Tag)
                {
                    t.Status = JobStatus.Completed;
                    t.CompletedDate = DateTime.Now;
                    jobsChanged = true;
                    return;
                }
            }
        }

        private void ActionFind(Object sender, EventArgs e)
        {
            FormFindJob form = new FormFindJob(ref listJobs);
            form.ShowDialog(this);
        }

        // Menu help
        private void ActionAbout(Object sender, EventArgs e)
        {
            FormAbout form = new FormAbout();
            form.ShowDialog(this);
        }

        // ListView context menu
        private void ActionContextMenu(Object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (listMain.FocusedItem.Bounds.Contains(e.Location) == true)
                {
                    listMainContextMenu.Show(Cursor.Position);
                }
            }
        }

        private void ActionJobDetails(Object sender, EventArgs e)
        {
            FormJobDetails form = new FormJobDetails(GetJobByKey((Int32)listMain.SelectedItems[0].Tag));
            form.ShowDialog(this);
        }

        // Tray icon
        private void ActionShowHide(Object sender, EventArgs e)
        {
            if (trayIcon.Visible == false)
            {
                Hide();
                ShowInTaskbar = false;
                trayIcon.Visible = true;
            }
            else
            {
                Show();
                ShowInTaskbar = true;
                trayIcon.Visible = false;
            }
        }

        /* Additional functions */
        private Int32 GetNumberOfJobs(JobStatus status)
        {
            Int32 res = 0;

            foreach (Job t in listJobs)
            {
                if (t.Status == status) res++;
            }

            return res;
        }

        private void AddItemInList(Job t)
        {
            ListViewItem item = new ListViewItem(t.Status.ToString());
            item.SubItems.Add(t.Title);
            item.Tag = t.Key;

            if (t.Termless)
            {
                item.SubItems.Add("Termless");
            }
            else
            {
                item.SubItems.Add(t.DeadlineDate.ToShortDateString());
            }

            listMain.Items.Add(item);
        }

        private void RemoveJobByKey(Int32 key)
        {
            foreach (Job t in listJobs)
            {
                if (t.Key == key)
                {
                    listJobs.Remove(t);
                    return;
                }
            }
        }

        private Job GetJobByKey(Int32 key)
        {
            foreach (Job t in listJobs)
            {
                if (t.Key == key)
                {
                    return t;
                }
            }

            return null;
        }

        /* Variables */
        private Config config;
        private ArrayList listJobs;
        private Boolean jobsChanged = false;
    }
}
