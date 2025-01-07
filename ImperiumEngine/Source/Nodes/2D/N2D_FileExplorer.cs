using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using ImGuiNET;
using ImperiumEngine.Source.Cores;
using ImperiumEngine.Source.Nodes._1D;
using ImperiumEngine.Source.Resources;

namespace ImperiumEngine.Source.Nodes._2D;

public class N2D_FileExplorer : ImpObject2D
{
    private N1D_MouseInput n_1DMouseInput = new N1D_MouseInput(); 
    
    private string dir_root;
    private string dir_current;
    private string selectedFile;
    private List<string> currentFiles = new List<string>();
    private List<string> currentDirectories = new List<string>();
    private Texture2D icon_file;
    private Texture2D icon_dir;
    
    // Add these fields to your class
    private HashSet<string> selectedItems = new HashSet<string>();
    public event Action<HashSet<string>> SelectionUpdated;
    
    float padding = 16.0f;
    float thumbnailSize = 128.0f;

    private int columnCount;
    //drag select

    
    public override void OnBegin()
    {
        //dir_current = Directory.GetDirectoryRoot();
        dir_current = ImpLib_File.GetDirectory_Content();
        selectedFile = string.Empty;
        
        icon_file = new Texture2D(ImpLib_File.GetFilepath_FromContent("Textures/icons/T_ico_file.PNG"));
        icon_dir = new Texture2D(ImpLib_File.GetFilepath_FromContent("Textures/icons/T_ico_folder.PNG"));
        
        //base.OnBegin();
    }

    public override void OnDraw(double delta)
    {
        ImGui.Begin(name);

        // Display current path
        ImGui.Text($"Current Path: {dir_current}");
        
        // -------------------------------------------------------------------
        // Top Row controls
        // -------------------------------------------------------------------
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(150f);
        
        // Up directory button
        if (ImGui.Button(".."))
        {
            DirectoryInfo parentDir = Directory.GetParent(dir_current);
            if (parentDir != null)
            {
                dir_current = parentDir.FullName;

                selectedFile = string.Empty;
            }
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f);
        ImGui.SliderFloat("Thumbnail Size", ref thumbnailSize, 32, 200);
       // ImGui.SameLine();
       // ImGui.SetNextItemWidth(150f);
       // ImGui.SliderFloat("Padding", ref padding, 0, 32);
        
        ImGui.EndGroup();
        
        // -------------------------------------------------------------------
        // File/Folder Grid
        // -------------------------------------------------------------------
        float cellSize = thumbnailSize + padding;
        float panelWidth = ImGui.GetContentRegionAvail().X;
        columnCount = (int)(panelWidth / cellSize);
        if (columnCount < 1) { columnCount = 1;}
           
        ImGui.Columns(columnCount,Convert.ToString(0),false);

        foreach (var d in new DirectoryInfo(dir_current).EnumerateFileSystemInfos())
        {
            string filename = d.Name;
            bool isSelected = selectedItems.Contains(filename);
            
            ImGui.PushID(filename);
            Texture2D icon = d.Attributes.HasFlag(FileAttributes.Directory) ? icon_dir : icon_file;

            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,ImpApp.config.UiColor_Selected);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button,ImpApp.config.UiColor_Selected);
            }
            ImGui.ImageButton(filename,icon.RendererID, new Vector2(thumbnailSize), new Vector2(0, 0), new Vector2(1, 1));

            if (ImGui.BeginDragDropSource())
            {
                string relativePath = d.FullName;
                byte[] pathBytes = Encoding.Unicode.GetBytes(relativePath);
                IntPtr ptr = Marshal.AllocHGlobal(pathBytes.Length);
                Marshal.Copy(pathBytes, 0, ptr, pathBytes.Length);
                ImGui.SetDragDropPayload("CONTENT_BROWSER_ITEM", ptr, (uint)pathBytes.Length);
                Marshal.FreeHGlobal(ptr);
                ImGui.EndDragDropSource();
            }
            
            ImGui.PopStyleColor();
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                if (!ImGui.GetIO().KeyCtrl)
                {
                    selectedItems.Clear();
                }

                if (selectedItems.Contains(filename))
                {
                    selectedItems.Remove(filename);
                }
                else
                {
                    selectedItems.Add(filename);
                }
                SelectionUpdated?.Invoke(selectedItems);
            }
            

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
               // dragStartPos = ImGui.GetMousePos();
                //isDragging = true;
            }
            
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                if (d.Attributes.HasFlag(FileAttributes.Directory))
                {
                    dir_current = Path.Combine(dir_current, d.Name);
                }
            }
            ImGui.TextWrapped(filename);
            ImGui.NextColumn();
            
            ImGui.PopStyleColor();
            ImGui.PopID();
        }
        
        ImGui.Columns(1);
        
        ImGui.End();
        
        base.OnDraw(delta);
    }
    
    private bool IsRectIntersecting(Vector2 r1Min, Vector2 r1Max, Vector2 r2Min, Vector2 r2Max)
    {
        return r1Min.X < r2Max.X && r1Max.X > r2Min.X &&
               r1Min.Y < r2Max.Y && r1Max.Y > r2Min.Y;
    }
    
    private Vector2 GetItemPosition(string filename)
    {
        float cellSize = thumbnailSize + padding;
        int itemCount = Array.IndexOf(new DirectoryInfo(dir_current).GetFileSystemInfos().Select(f => f.Name).ToArray(), filename);
        int itemColumn = itemCount % columnCount;
        int itemRow = itemCount / columnCount;
   
        Vector2 windowPos = ImGui.GetWindowPos();
        Vector2 contentStart = windowPos + ImGui.GetWindowContentRegionMin();
   
        return new Vector2(
            contentStart.X + itemColumn * cellSize,
            contentStart.Y + itemRow * cellSize
        );
    }

    
}