---@class lvl_test : Imp3D
local a = {}

local list_root

function a:OnBegin()
    Log_Info("lvl_test OnBegin")

    ---@type C2_Button
    local btn = self.Child_Get("Button")
    btn.on_click.Add(function()
        Log_Info("   click button")
    end)


    ---@type C2_OptionList
    list_root = self.Child_Get("list_root")
    list_root.on_option_select.Add(function(c, i)
        Log_Info("   select option: " .. tostring(i))
        
    end)
end

function a:OnUpdate(dt)
end



return a
