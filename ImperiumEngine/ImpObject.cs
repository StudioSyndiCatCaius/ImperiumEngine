
using ImGuiNET;

namespace ImperiumEngine
{
// ========================================================================================
// Object (Basic)
// ========================================================================================
    public class ImpObject
    {
        public ImpObject owner;
        public string name;
        
        public virtual void OnBegin()
        {

        }

        public virtual void OnEnd()
        {

        }

        public virtual void OnUpdate(double delta)
        {

        }

        public virtual void OnDraw(double delta)
        {

        }

        // ----------------------------------------------------------------
        // Tags
        // ----------------------------------------------------------------
        public List<string> tags;

        public bool HasTag(string tag)
        {
            return tags.Contains(tag);
        }

        public void GiveTag(string tag, bool bGive)
        {
            if (bGive && !tags.Contains(tag))
            {
                tags.Add(tag);
                return;
            }
            if (!bGive && tags.Contains(tag))
            {
                tags.Remove(tag);
                return;
            }
        }
        
        // ----------------------------------------------------------------
        // CHILDREN
        // ----------------------------------------------------------------

        public List<ImpObject> GetChildren_All(bool bIncludeDescendants = false)
        {
            List<ImpObject> output = new List<ImpObject>();
            foreach (var i in ImpApp.GetObjects_Active())
            {
                if (i.owner == this)
                {
                    output.Add(i);
                }
            }

            return output;
        }
    }

// ========================================================================================
// Object (Basic)
// ========================================================================================
    public class ImpObject2D : ImpObject
    {
        public FTransform2D LocalTransform;
        public FVector4D Margins;

        public override void OnDraw(double delta)
        {
            OnDraw2D(delta);
            base.OnDraw(delta);
        }

        public virtual void OnDraw2D(double delta)
        {
            
        }


    }

    public class ImpObject3D : ImpObject
    {
        public FTransform3D LocalTransform;


    }
}