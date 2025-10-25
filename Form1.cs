using Microsoft.Playwright;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text.Json;

namespace asda
{
    // =================================================================================
    // MODEL (Represents a single shopping list item)
    // =================================================================================

    public partial class Form1 : Form
    {
        // --- Browser ---
        IPlaywright? playwright;
        IBrowser? browser;
        IPage? page;

        // --- Persistence ---
        private readonly string _saveFilePath;

        public Form1()
        {
            // Define the path for the JSON save file in the user's local app data folder
            _saveFilePath = Path.Combine(Application.LocalUserAppDataPath, "tasks.json");

            InitializeComponent();

            LoadItems();

            if (Screen.PrimaryScreen is not null)
            {
                StartBrowser(Screen.PrimaryScreen);
            }
        }

        private async void StartBrowser(Screen screen)
        {
            try
            {
                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Args = ["--disable-blink-features=AutomationControlled", "--start-maximized"],
                    Headless = false
                });
                page = await browser.NewPageAsync();
                int primaryWidth = screen.Bounds.Width;
                int primaryHeight = screen.Bounds.Height;
                await page.SetViewportSizeAsync(primaryWidth, primaryHeight);
                await page.GotoAsync("https://www.asda.com/");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playwright error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadItems()
        {
            if (File.Exists(_saveFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_saveFilePath);
                    // Use System.Text.Json to deserialize the file content into a list of TodoItems
                    var tasks = JsonSerializer.Deserialize<List<Item>>(json);
                    if (tasks != null)
                    {
                        PopulateTreeView(treeView1.Nodes, tasks);
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

        private void SaveItems()
        {
            try
            {
                // Convert the current state of the TreeView back into a list of TodoItem objects
                var tasks = ConvertNodesToItems(treeView1.Nodes);
                // Configure serializer for pretty printing the JSON
                JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
                var json = JsonSerializer.Serialize(tasks, jsonSerializerOptions);
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
        private static List<Item> ConvertNodesToItems(TreeNodeCollection nodes)
        {
            var items = new List<Item>();
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is Item item)
                {
                    // Recursively convert children nodes and update the sub-tasks list
                    item.SubTasks = ConvertNodesToItems(node.Nodes);
                    items.Add(item);
                }
            }
            return items;
        }

        // --- Event Handlers ---
        private void TreeView_DoubleClick(object sender, EventArgs e)
        {
            var m = (MouseEventArgs)e;
            Point targetPoint = new(m.X, m.Y);
            TreeNode targetNode = treeView1.GetNodeAt(targetPoint);

            // Open this item in browser window
            if (targetNode != null && targetNode.Tag is Item item && !string.IsNullOrWhiteSpace(item.URL))
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

        private void AddItemButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBox1.Text) && treeView1.SelectedNode != null)
            {
                var parentNode = treeView1.SelectedNode;
                if (parentNode is not null)
                {
                    var parentItem = parentNode.Tag as Item;
                    if (parentItem is not null)
                    {
                        var pageTitle = textBox1.Text;

                        var newItem = new Item(pageTitle);
                        var newNode = new TreeNode(newItem.Title) { Tag = newItem };

                        // Add to the UI
                        parentNode.Nodes.Add(newNode);
                        // Add to the data model
                        parentItem.SubTasks.Add(newItem);

                        parentNode.Expand(); // Show the new sub-task
                        textBox1.Clear();
                        SaveItems(); // Save changes
                    }
                }
            }
        }

        private async void AddSubItemButton_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                var parentNode = treeView1.SelectedNode;
                var parentItem = parentNode.Tag as Item;

                var pageTitle = await page.TitleAsync();
                var pageURL = page.Url;

                var newItem = new Item(pageTitle)
                {
                    URL = pageURL
                };
                var newNode = new TreeNode(newItem.Title) { Tag = newItem };

                // Add to the UI
                parentNode.Nodes.Add(newNode);
                // Add to the data model
                parentItem.SubTasks.Add(newItem);

                parentNode.Expand(); // Show the new sub-task
                textBox1.Clear();
                SaveItems(); // Save changes
            }
        }

        private void RemoveItemButton_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                var selectedNode = treeView1.SelectedNode;
                var parentNode = selectedNode.Parent;
                var dataItem = selectedNode.Tag as Item;

                if (parentNode != null)
                {
                    if (parentNode.Tag is Item parentItem && dataItem is not null)
                    {
                        parentItem.SubTasks.Remove(dataItem); // Remove from data model
                    }
                }

                selectedNode.Remove(); // Remove from UI
                SaveItems(); // Save changes
            }
        }

        /// <summary>
        /// Syncs the IsDone state between the TreeNode's checked state and the data model.
        /// </summary>
        private void TreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null && e.Node.Tag is Item item)
            {
                item.IsDone = e.Node.Checked;
                SaveItems(); // Save changes
            }
        }

        // --- Drag and Drop Event Handlers ---
        private void TreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        private void TreeView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));
            treeView1.SelectedNode = treeView1.GetNodeAt(targetPoint);
        }

        private void TreeView_DragDrop(object sender, DragEventArgs e)
        {
            if (!(e.Data.GetData(typeof(TreeNode)) is TreeNode sourceNode))
                return;

            Point targetPoint = treeView1.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = treeView1.GetNodeAt(targetPoint);

            // Case 1: Dropped onto an empty area, make it a root node.
            if (targetNode == null)
            {
                sourceNode.Remove();
                treeView1.Nodes.Add(sourceNode);
                treeView1.SelectedNode = sourceNode;
                SaveItems();
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
                TreeNodeCollection collection = targetNode.Parent?.Nodes ?? treeView1.Nodes;
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

            treeView1.SelectedNode = sourceNode;
            SaveItems();
        }
        private static bool ContainsNode(TreeNode node1, TreeNode node2)
        {
            if (node2.Parent == null) return false;
            if (node2.Parent == node1) return true;
            return ContainsNode(node1, node2.Parent);
        }

        private void LoadSampleData()
        {
            var root2 = new Item("Shopping lists");
            var dairy = new Item("Dairy");
            dairy.SubTasks.Add(new Item("Milk"));
            dairy.SubTasks.Add(new Item("Cheese"));
            root2.SubTasks.Add(dairy);
            var bakery = new Item("Bakery");
            bakery.SubTasks.Add(new Item("Bread"));
            root2.SubTasks.Add(bakery);

            var allTasks = new List<Item> { root2 };

            PopulateTreeView(treeView1.Nodes, allTasks);
            //taskTreeView.ExpandAll();
        }

        /// <summary>
        /// Recursively populates the TreeView with TreeNode objects from the data model.
        /// </summary>
        private static void PopulateTreeView(TreeNodeCollection parentNodes, List<Item> items)
        {
            foreach (var item in items)
            {

                var quantText = item.Quantity == 1 ? "": "(" + item.Quantity.ToString() + ") ";
                var node = new TreeNode(quantText + item.Title)
                {
                    Tag = item, // Store the data object in the Tag property
                    Checked = item.IsDone
                };
                parentNodes.Add(node);
                // If there are sub-tasks, recurse
                if (item.SubTasks.Count > 0)
                {
                    PopulateTreeView(node.Nodes, item.SubTasks);
                }
            }
        }
    }

    public class Item {
        public string Title { get; set; }
        public string? URL { get; set; } = null;
        public bool IsDone { get; set; }
        public int Quantity { get; set; }
        public List<Item> SubTasks { get; set; }
        public Item(string title, string? url = null)
        {
            Title = title;
            URL = url;
            IsDone = false;
            Quantity = 1;
            SubTasks = [];
        }
    }

}
