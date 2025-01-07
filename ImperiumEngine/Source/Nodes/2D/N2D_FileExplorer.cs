using ImGuiNET;

namespace ImperiumEngine.Source.Nodes._2D;

public class N2D_FileExplorer : ImpObject2D
{
    public override void OnBegin()
    {
        currentPath = Directory.GetCurrentDirectory();
        selectedFile = string.Empty;
        RefreshContent();
        base.OnBegin();
    }

    public override void OnDraw(double delta)
    {
        ImGui.Begin(name);

        // Display current path
        ImGui.Text($"Current Path: {currentPath}");
        
        // Up directory button
        if (ImGui.Button(".."))
        {
            DirectoryInfo parentDir = Directory.GetParent(currentPath);
            if (parentDir != null)
            {
                currentPath = parentDir.FullName;
                RefreshContent();
                selectedFile = string.Empty;
            }
        }

        ImGui.Separator();

        // Display directories
        if (ImGui.BeginChild("Directories", new System.Numerics.Vector2(0, 200)))
        {
            foreach (string dir in currentDirectories)
            {
                string dirName = Path.GetFileName(dir);
                if (ImGui.Selectable($"📁 {dirName}", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseDoubleClicked(0))
                    {
                        currentPath = dir;
                        RefreshContent();
                        selectedFile = string.Empty;
                    }
                }
            }

            // Display files
            foreach (string file in currentFiles)
            {
                string fileName = Path.GetFileName(file);
                bool isSelected = selectedFile == file;
                
                if (ImGui.Selectable($"📄 {fileName}", isSelected))
                {
                    selectedFile = file;
                }
            }
        }
        ImGui.EndChild();

        // Display selected file
        if (!string.IsNullOrEmpty(selectedFile))
        {
            ImGui.Separator();
            ImGui.Text($"Selected: {Path.GetFileName(selectedFile)}");
            
            if (ImGui.Button("Open"))
            {
                // Handle file opening here
                Console.WriteLine($"Opening file: {selectedFile}");
            }
        }

        ImGui.End();
        
        base.OnDraw(delta);
    }
    
    private string currentPath;
    private string selectedFile;
    private List<string> currentFiles = new List<string>();
    private List<string> currentDirectories = new List<string>();
    

    private void RefreshContent()
    {
        try
        {
            currentFiles = new List<string>(Directory.GetFiles(currentPath));
            currentDirectories = new List<string>(Directory.GetDirectories(currentPath));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error accessing directory: {e.Message}");
            currentFiles = new List<string>();
            currentDirectories = new List<string>();
        }
    }
    

    public string GetSelectedFile()
    {
        return selectedFile;
    }
}