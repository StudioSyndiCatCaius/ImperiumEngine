using System;
using ImperiumEngine.Source.Renderers;



namespace ImperiumEngine
{
    class Program
    {
        public static void Main(string[] args)
        {
            ImpApp app = new Imperium_Editor();
            app.Start();
        }
    }

    // ####################################################################################################################################
    // IMPERIUM APP
    // ####################################################################################################################################
    // An application process (Editor, Game, etc.)
    public class ImpApp
    {
        
        // ================================================================================
        // Objects
        // ================================================================================
        private static List<ImpObject> impObj_toStart = new List<ImpObject>();
        private List<ImpObject> impObj_active = new List<ImpObject>();
        private ImpObject root_object=new ImpObject();
        public static T Object_Create<T>(string name, ImpObject owner) where T : ImpObject, new()
        {
            T obj = new T();

            obj.owner = owner;
            obj.name = name;
            impObj_toStart.Add(obj);
            
            Console.WriteLine("Create new Object: "+obj);
            return obj;
        }

        // ================================================================================
        // Lifetime
        // ================================================================================
        public void Start()
        {
            Init(root_object);
            ImpCore impCore = new ImpCore_OpenGL();
            impCore.owner_app = this;
            impCore.Window_Create();
        }

        public virtual void Init(ImpObject root)
        {
            
        }

        public virtual void Update(double delta)
        {
            // start pending objects
            foreach (var i in impObj_toStart.ToList())
            {
                i.OnBegin();
                impObj_active.Add(i);
            }
            impObj_toStart.Clear();
            
            //update active objects
            foreach (var i in impObj_active)
            {
                i.OnUpdate(delta);
            }
        }

        public virtual void Draw(double delta)
        {
            foreach (var i in impObj_active)
            {
                i.OnDraw(delta);
            }
        }

        public virtual void End()
        {
            
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }
    }
    
    // ####################################################################################################################################
    // IMPERIUM RENDER COR
    // ####################################################################################################################################

    //The render core used for this app (OpenGl, Vulkan, etc.)
    public class ImpCore
    {
        public ImpApp owner_app;
        public virtual void Window_Create()
        {
            
        }

        public void Core_Update(double delta)
        {
            owner_app.Update(delta);
        }
        public void Core_Draw(double delta)
        {
            owner_app.Draw(delta);
        }
    }
    
    public static class ImpConfig
    {
        public static string AppName = "Imperium Engine";
    }
    
    // ####################################################################################################################################
    // IMPERIUM OBJECTS
    // ####################################################################################################################################
    
    
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
    }
    
    // ========================================================================================
    // Object (Basic)
    // ========================================================================================
    public class ImpObject2D : ImpObject
    {
        
    }
    
    public class ImpObject3D : ImpObject
    {
        
    }
}

