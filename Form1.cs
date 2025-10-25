using Microsoft.Playwright;
using System.Security.Policy;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ToDo
{
    // =================================================================================
    // MODEL (Represents a single to-do item)
    // =================================================================================
    public class TodoItem
    {
        public string Title { get; set; }
        public string? URL { get; set; } = null;
        public bool IsDone { get; set; }
        public List<TodoItem> SubTasks { get; set; }

        public TodoItem(string title, string? url = null)
        {
            Title = title;
            URL = url;
            IsDone = false;
            SubTasks = [];
        }
    }

    // =================================================================================
    // VIEW (The Main Form)
    // This class creates the UI controls and handles all user interaction events.
    // =================================================================================
    public partial class TodoForm : Form
    {
        IPlaywright playwright;
        IBrowser browser;
        IPage page;

        // --- UI Controls ---
        private TreeView? taskTreeView;
        private TextBox? newTaskTextBox;
        private TextBox? newTaskURLTextBox;
        private Button? addTaskButton;
        private Button? addSubTaskButton;
        private Button? removeTaskButton;
        private Label? newTaskLabel;
        private Label? newTaskURLLabel;

        // --- Persistence ---
        private readonly string _saveFilePath;

        public TodoForm()
        {
            // --- Form Setup ---
            Text = "ASDA Shopping Lists";
            Size = new Size(450, 600);
            MinimumSize = new Size(400, 500);
            BackColor = Color.FromArgb(240, 242, 245);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            TopMost = true;

            // Define the path for the JSON save file in the user's local app data folder
            _saveFilePath = Path.Combine(Application.LocalUserAppDataPath, "tasks.json");

            InitializeComponents();
            LoadTasks();

            StartBrowser();
        }

        private async void StartBrowser()
        {
            try
            {
                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions {
                    Args=["--disable-blink-features=AutomationControlled", "--start-maximized"],
                    Headless = false 
                });
                page = await browser.NewPageAsync();
                int primaryWidth = Screen.PrimaryScreen.Bounds.Width;
                int primaryHeight = Screen.PrimaryScreen.Bounds.Height;
                await page.SetViewportSizeAsync(primaryWidth, primaryHeight);
                await page.GotoAsync("https://www.asda.com/");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playwright error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadTasks()
        {
            if (File.Exists(_saveFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_saveFilePath);
                    // Use System.Text.Json to deserialize the file content into a list of TodoItems
                    var tasks = JsonSerializer.Deserialize<List<TodoItem>>(json);
                    if (tasks != null)
                    {
                        PopulateTreeView(taskTreeView.Nodes, tasks);
                        //taskTreeView.ExpandAll();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading tasks: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Fallback to sample data if loading fails
                    LoadSampleData();
                }
            }
            else
            {
                // If no save file exists, load sample data for the first run.
                LoadSampleData();
            }
        }

        private void SaveTasks()
        {
            try
            {
                // Convert the current state of the TreeView back into a list of TodoItem objects
                var tasks = ConvertNodesToItems(taskTreeView.Nodes);
                // Configure serializer for pretty printing the JSON
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(tasks, options);
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving tasks: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Recursively converts a TreeNodeCollection to a list of TodoItem objects.
        /// </summary>
        private List<TodoItem> ConvertNodesToItems(TreeNodeCollection nodes)
        {
            var items = new List<TodoItem>();
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is TodoItem item)
                {
                    // Recursively convert children nodes and update the sub-tasks list
                    item.SubTasks = ConvertNodesToItems(node.Nodes);
                    items.Add(item);
                }
            }
            return items;
        }

        /// <summary>
        /// Creates, configures, and positions all the UI controls on the form.
        /// </summary>
        private void InitializeComponents()
        {
            // --- TreeView for Tasks ---
            taskTreeView = new TreeView
            {
                Location = new Point(15, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Size = new Size(ClientSize.Width - 30, ClientSize.Height - 190),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F),
                AllowDrop = true,
                CheckBoxes = true // Enable checkboxes on tree nodes
            };
            // Event handler for when a checkbox is checked/unchecked
            taskTreeView.AfterCheck += TaskTreeView_AfterCheck;
            taskTreeView.ItemDrag += TaskTreeView_ItemDrag;
            taskTreeView.DragEnter += TaskTreeView_DragEnter;
            taskTreeView.DragOver += TaskTreeView_DragOver;
            taskTreeView.DragDrop += TaskTreeView_DragDrop;
            taskTreeView.DoubleClick += TaskTreeView_DoubleClick;

            // --- New Task Label ---
            newTaskLabel = new Label
            {
                Text = "New Heading",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(96, 103, 112),
                Location = new Point(12, ClientSize.Height - 135),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                AutoSize = true
            };

            // --- New Task TextBox ---
            newTaskTextBox = new TextBox
            {
                Location = new Point(15, ClientSize.Height - 115),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Size = new Size(ClientSize.Width - 30, 23),
                Font = new Font("Segoe UI", 10F),
                AllowDrop = true
            };

            newTaskTextBox.DragEnter += NewTaskTextBox_DragEnter;
            newTaskTextBox.DragDrop += NewTaskTextBox_DragDrop;

            // --- Add Task Button ---
            addTaskButton = new Button
            {
                Text = "Add Heading",
                Location = new Point(15, ClientSize.Height - 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Size = new Size(125, 25),
                BackColor = Color.FromArgb(24, 119, 242),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            addTaskButton.FlatAppearance.BorderSize = 0;
            addTaskButton.Click += AddTaskButton_Click;

            // --- Add Sub-Task Button ---
            addSubTaskButton = new Button
            {
                Text = "Add Product",
                Location = new Point(150, ClientSize.Height - 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Size = new Size(125, 25),
                BackColor = Color.FromArgb(24, 119, 242),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            addSubTaskButton.FlatAppearance.BorderSize = 0;
            addSubTaskButton.Click += AddSubTaskButton_Click;

            // --- Remove Task Button ---
            removeTaskButton = new Button
            {
                Text = "Remove Selected",
                Location = new Point(285, ClientSize.Height - 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Size = new Size(125, 25),
                BackColor = Color.FromArgb(228, 230, 235),
                ForeColor = Color.FromArgb(75, 79, 86),
                FlatStyle = FlatStyle.Flat,
            };
            removeTaskButton.FlatAppearance.BorderSize = 0;
            removeTaskButton.Click += RemoveTaskButton_Click;

            // Add all controls to the form's control collection
            Controls.Add(taskTreeView);
            Controls.Add(newTaskLabel);
            Controls.Add(newTaskTextBox);
            Controls.Add(addTaskButton);
            Controls.Add(addSubTaskButton);
            Controls.Add(removeTaskButton);
        }

        // --- Event Handlers ---

        private void NewTaskTextBox_DragEnter(object sender, DragEventArgs e)
        {
            // Check if the data being dragged is text (links are passed as text).
            // If so, show the copy cursor to indicate a valid drop target.
            e.Effect = e.Data.GetDataPresent(DataFormats.Text) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void NewTaskTextBox_DragDrop(object sender, DragEventArgs e)
        {
            // Get the dragged text data.
            object textData = e.Data.GetData(DataFormats.Text);
            if (textData != null)
            {
                string text = textData.ToString();

                // Web browsers often drop multiple lines of text (like the page title and the URL).
                // We will find the line that actually starts with "http" to get the link.
                var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                var url = lines.FirstOrDefault(line => line.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase));

                HttpClient sharedClient = new() { BaseAddress = new Uri(url) };
                sharedClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
                HttpResponseMessage response = await sharedClient.GetAsync(url);
                string source = await response.Content.ReadAsStringAsync();
                string title = Regex.Match(source, @"\<title\b[^>]*\>\s*(?<Title>[\s\S]*?)\</title\>", RegexOptions.IgnoreCase).Groups["Title"].Value;

                // If a URL is found, use that. Otherwise, just use the first line of whatever text was dropped.
                newTaskTextBox.Text = title;
                newTaskURLTextBox.Text = url;
            }
        }

        private void TaskTreeView_DoubleClick(object? sender, EventArgs e)
        {
            var m = (MouseEventArgs)e;
            Point targetPoint = new Point(m.X, m.Y);
            TreeNode targetNode = taskTreeView.GetNodeAt(targetPoint);

            // Open this item in browser window
            if (targetNode != null && targetNode.Tag is TodoItem item && !string.IsNullOrWhiteSpace(item.URL))
            {
                try
                {
                    page.GotoAsync(item.URL);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Playwright error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void AddTaskButton_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(newTaskTextBox.Text) && taskTreeView.SelectedNode != null)
            {
                var parentNode = taskTreeView.SelectedNode;
                var parentItem = parentNode.Tag as TodoItem;

                var pageTitle = newTaskTextBox.Text;

                var newItem = new TodoItem(pageTitle);
                var newNode = new TreeNode(newItem.Title) { Tag = newItem };

                // Add to the UI
                parentNode.Nodes.Add(newNode);
                // Add to the data model
                parentItem.SubTasks.Add(newItem);

                parentNode.Expand(); // Show the new sub-task
                newTaskTextBox.Clear();
                SaveTasks(); // Save changes
            }
        }

        private async void AddSubTaskButton_Click(object? sender, EventArgs e)
        {
            if (taskTreeView.SelectedNode != null)
            {
                var parentNode = taskTreeView.SelectedNode;
                var parentItem = parentNode.Tag as TodoItem;

                var pageTitle = await page.TitleAsync();
                var pageURL = page.Url;

                var newItem = new TodoItem(pageTitle);
                newItem.URL = pageURL;
                var newNode = new TreeNode(newItem.Title) { Tag = newItem };

                // Add to the UI
                parentNode.Nodes.Add(newNode);
                // Add to the data model
                parentItem.SubTasks.Add(newItem);

                parentNode.Expand(); // Show the new sub-task
                newTaskTextBox.Clear();
                SaveTasks(); // Save changes
            }
        }

        private void RemoveTaskButton_Click(object? sender, EventArgs e)
        {
            if (taskTreeView.SelectedNode != null)
            {
                var selectedNode = taskTreeView.SelectedNode;
                var parentNode = selectedNode.Parent;
                var dataItem = selectedNode.Tag as TodoItem;

                if (parentNode != null)
                {
                    var parentItem = parentNode.Tag as TodoItem;
                    parentItem.SubTasks.Remove(dataItem); // Remove from data model
                }

                selectedNode.Remove(); // Remove from UI
                SaveTasks(); // Save changes
            }
        }

        /// <summary>
        /// Syncs the IsDone state between the TreeNode's checked state and the data model.
        /// </summary>
        private void TaskTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null && e.Node.Tag is TodoItem item)
            {
                item.IsDone = e.Node.Checked;
                SaveTasks(); // Save changes
            }
        }

        // --- Drag and Drop Event Handlers ---
        private void TaskTreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void TaskTreeView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void TaskTreeView_DragOver(object sender, DragEventArgs e)
        {
            Point targetPoint = taskTreeView.PointToClient(new Point(e.X, e.Y));
            taskTreeView.SelectedNode = taskTreeView.GetNodeAt(targetPoint);
        }

        private void TaskTreeView_DragDrop(object sender, DragEventArgs e)
        {
            if (!(e.Data.GetData(typeof(TreeNode)) is TreeNode sourceNode))
                return;

            Point targetPoint = taskTreeView.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = taskTreeView.GetNodeAt(targetPoint);

            // Case 1: Dropped onto an empty area, make it a root node.
            if (targetNode == null)
            {
                sourceNode.Remove();
                taskTreeView.Nodes.Add(sourceNode);
                taskTreeView.SelectedNode = sourceNode;
                SaveTasks();
                return;
            }

            // Case 2: Validation - Cannot drop a node on itself or on one of its children.
            if (sourceNode == targetNode || ContainsNode(sourceNode, targetNode))
            {
                return;
            }

            bool areSiblings = sourceNode.Parent == targetNode.Parent;

            if (areSiblings)
            {
                // --- Sibling Reorder Logic ---
                TreeNodeCollection collection = targetNode.Parent?.Nodes ?? taskTreeView.Nodes;
                int sourceOriginalIndex = collection.IndexOf(sourceNode);
                int targetOriginalIndex = collection.IndexOf(targetNode);

                sourceNode.Remove();

                if (sourceOriginalIndex > targetOriginalIndex)
                {
                    // Moving UP: Dropped on a preceding sibling, so insert BEFORE it.
                    collection.Insert(targetOriginalIndex, sourceNode);
                }
                else
                {
                    // Moving DOWN: Dropped on a following sibling, so insert AFTER it.
                    int newTargetIndex = collection.IndexOf(targetNode);
                    if (newTargetIndex == collection.Count - 1)
                    {
                        collection.Add(sourceNode);
                    }
                    else
                    {
                        collection.Insert(newTargetIndex + 1, sourceNode);
                    }
                }
            }
            else
            {
                // Add as a CHILD of the target node.
                sourceNode.Remove();
                targetNode.Nodes.Add(sourceNode);
                targetNode.Expand();
            }

            taskTreeView.SelectedNode = sourceNode;
            SaveTasks();
        }

        private bool ContainsNode(TreeNode node1, TreeNode node2)
        {
            if (node2.Parent == null) return false;
            if (node2.Parent == node1) return true;
            return ContainsNode(node1, node2.Parent);
        }

        // --- Data Loading ---

        private void LoadSampleData()
        {
            var root2 = new TodoItem("Shopping lists");
            var dairy = new TodoItem("Dairy");
            dairy.SubTasks.Add(new TodoItem("Milk"));
            dairy.SubTasks.Add(new TodoItem("Cheese"));
            root2.SubTasks.Add(dairy);
            var bakery = new TodoItem("Bakery");
            bakery.SubTasks.Add(new TodoItem("Bread"));
            root2.SubTasks.Add(bakery);

            var allTasks = new List<TodoItem> { root2 };

            PopulateTreeView(taskTreeView.Nodes, allTasks);
            //taskTreeView.ExpandAll();
        }

        /// <summary>
        /// Recursively populates the TreeView with TreeNode objects from the data model.
        /// </summary>
        private static void PopulateTreeView(TreeNodeCollection parentNodes, List<TodoItem> tasks)
        {
            foreach (var task in tasks)
            {
                var node = new TreeNode(task.Title)
                {
                    Tag = task, // Store the data object in the Tag property
                    // Checked = task.IsDone
                };
                parentNodes.Add(node);
                // If there are sub-tasks, recurse
                if (task.SubTasks.Count > 0)
                {
                    PopulateTreeView(node.Nodes, task.SubTasks);
                }
            }
        }
    }

    // =================================================================================
    // APPLICATION ENTRY POINT
    // =================================================================================
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TodoForm());
        }
    }
}
