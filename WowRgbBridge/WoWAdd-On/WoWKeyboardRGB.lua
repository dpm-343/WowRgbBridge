--------------------------------------------------
-- WoW Keyboard RGB
-- WoW: Forever (Interface 16001)
--------------------------------------------------

local addonName, ns = ...

--------------------------------------------------
-- Saved Variables
--------------------------------------------------

WoWKeyboardRGBDB = WoWKeyboardRGBDB or {}

--------------------------------------------------
-- Configuration
--------------------------------------------------

local BOX_SIZE = 4
local BOX_COUNT = 12

local holder
local boxes = {}
local sigBoxes = {}

--------------------------------------------------
-- Position Management
--------------------------------------------------

local function GetAnchor()

    local corner = WoWKeyboardRGBDB.corner or "TOPLEFT"

    if corner == "TOPLEFT" then
        return "TOPLEFT", 5, -5

    elseif corner == "TOPRIGHT" then
        return "TOPRIGHT", -5, -5

    elseif corner == "BOTTOMRIGHT" then
        return "BOTTOMRIGHT", -5, 5
    end

    return "BOTTOMLEFT", 5, 5
end

local function ApplyPosition()

    if not holder then
        return
    end

    holder:ClearAllPoints()

    local point, x, y = GetAnchor()

    holder:SetPoint(point, UIParent, point, x, y)
end

--------------------------------------------------
-- Action Slot Status
--------------------------------------------------

local COLORS = {
    READY      = {0, 1, 0},      -- Green
    ALMOST     = {1, 1, 0},      -- Yellow (<3 sec cooldown)
    COOLDOWN   = {1, 0, 0},      -- Red
    RANGE      = {0.78, 0.325, 0},    -- Orange
    RESOURCE   = {1, 0, 1},      -- Purple
    ACTIVE     = {1, 1, 1},      -- White (reserved)
    UNKNOWN    = {0, 0, 1},      -- Blue
    EMPTY      = {0, 0, 0},      -- Black
    SIG1       = { 1, 0, 0 }, 
    SIG2       = { 0, 1, 0 }, 
    SIG3       = { 0, 0, 1 } ,
    TERM       = { 1, 1, 1}
}

local function GetSlotColor(slot)

    if not HasAction(slot) then
        return unpack(COLORS.EMPTY)
    end

    local actionType, actionID = GetActionInfo(slot)

    if actionType == "spell" and actionID then

        local cooldown = C_Spell.GetSpellCooldown(actionID)

        if cooldown and cooldown.isActive then

            -- local remaining = cooldown.timeUntilEndOfStartRecovery or 0

            -- if remaining > 0 and remaining < 3 then
            --     return unpack(COLORS.ALMOST)
            -- end

            return unpack(COLORS.COOLDOWN)
        end
    end

    local usable, noResource = IsUsableAction(slot)

    if noResource then
        return unpack(COLORS.RESOURCE)
    end

    local inRange = IsActionInRange(slot)

    if inRange == false then
        return unpack(COLORS.RANGE)
    end

    if usable then
        return unpack(COLORS.READY)
    end

    return unpack(COLORS.UNKNOWN)
end

--------------------------------------------------
-- UI Creation
--------------------------------------------------

local function CreateBoxes()

    holder = CreateFrame("Frame", nil, UIParent)

    holder:SetSize(
        -- Four signal boxes precede the action boxes plus one footer
        BOX_SIZE * (BOX_COUNT + 5),
        BOX_SIZE
    )

    ApplyPosition()

    --------------------------------------------------
    -- Border
    --------------------------------------------------

    local borderColor = {0, 0.1, 0.3, 1} -- Dark blue

    local topBorder = holder:CreateTexture(nil, "OVERLAY")
    topBorder:SetColorTexture(unpack(borderColor))
    topBorder:SetPoint("TOPLEFT", holder, "TOPLEFT", -1, 1)
    topBorder:SetPoint("TOPRIGHT", holder, "TOPRIGHT", 1, 1)
    topBorder:SetHeight(1)

    local bottomBorder = holder:CreateTexture(nil, "OVERLAY")
    bottomBorder:SetColorTexture(unpack(borderColor))
    bottomBorder:SetPoint("BOTTOMLEFT", holder, "BOTTOMLEFT", -1, -1)
    bottomBorder:SetPoint("BOTTOMRIGHT", holder, "BOTTOMRIGHT", 1, -1)
    bottomBorder:SetHeight(1)

    local leftBorder = holder:CreateTexture(nil, "OVERLAY")
    leftBorder:SetColorTexture(unpack(borderColor))
    leftBorder:SetPoint("TOPLEFT", holder, "TOPLEFT", -1, 0)
    leftBorder:SetPoint("BOTTOMLEFT", holder, "BOTTOMLEFT", -1, 0)
    leftBorder:SetWidth(1)

    local rightBorder = holder:CreateTexture(nil, "OVERLAY")
    rightBorder:SetColorTexture(unpack(borderColor))
    rightBorder:SetPoint("TOPRIGHT", holder, "TOPRIGHT", 1, 0)
    rightBorder:SetPoint("BOTTOMRIGHT", holder, "BOTTOMRIGHT", 1, 0)
    rightBorder:SetWidth(1)

    for slot = 1, BOX_COUNT + 5 do

        local tex = holder:CreateTexture(
            nil,
            "OVERLAY"
        )

        tex:SetSize(
            BOX_SIZE,
            BOX_SIZE
        )

        tex:SetPoint(
            "LEFT",
            holder,
            "LEFT",
            (slot - 1) * BOX_SIZE,
            0
        )

        if slot <= 4 or slot == (BOX_COUNT + 5) then
            sigBoxes[slot] = tex

            local color = COLORS.EMPTY
            if slot == 1 then
                color = COLORS.SIG1
            elseif slot == 2 then
                color = COLORS.SIG2
            elseif slot == 3 then
                color = COLORS.SIG3
            elseif slot == (BOX_COUNT + 5) then
                -- Add termination box at the end of the strip
                color = COLORS.TERM
            end

            tex:SetColorTexture(color[1], color[2], color[3], 1)
        else
            boxes[slot - 4] = tex
            tex:SetColorTexture(0, 0, 0, 1)
        end
    end

end

--------------------------------------------------
-- Updates
--------------------------------------------------

local function UpdateBoxes()

    for slot = 1, BOX_COUNT do

        local r, g, b = GetSlotColor(slot)

        if boxes[slot] then
            boxes[slot]:SetColorTexture(
                r,
                g,
                b,
                1
            )
        end
    end
end

--------------------------------------------------
-- Slash Commands
--------------------------------------------------

SLASH_WOWKEYBOARDRGB1 = "/rgb"

SlashCmdList["WOWKEYBOARDRGB"] = function(msg)

    msg = string.upper(msg or "")

    if msg == "TL" then

        WoWKeyboardRGBDB.corner = "TOPLEFT"

    elseif msg == "TR" then

        WoWKeyboardRGBDB.corner = "TOPRIGHT"

    elseif msg == "BL" then

        WoWKeyboardRGBDB.corner = "BOTTOMLEFT"

    elseif msg == "BR" then

        WoWKeyboardRGBDB.corner = "BOTTOMRIGHT"

    elseif msg == "TEST" then

        print("WoWKeyboardRGB Test")
        print("Corner: " .. (WoWKeyboardRGBDB.corner or "TOPLEFT"))

        return

    else

        print("WoWKeyboardRGB Commands:")
        print("/rgb tl")
        print("/rgb tr")
        print("/rgb bl")
        print("/rgb br")
        print("/rgb test")
        print("/rgb pos")

        return
    end

    ApplyPosition()

    print("WoWKeyboardRGB moved to "
        .. WoWKeyboardRGBDB.corner)
end

--------------------------------------------------
--Debug Slash Command
--------------------------------------------------
SLASH_RGBDEBUG1 = "/rgbdebug"

SlashCmdList["RGBDEBUG"] = function()

    local startTime, duration, enable = GetActionCooldown(10)

    print("startTime type =", type(startTime))
    print("duration type  =", type(duration))
    print("enable type    =", type(enable))

    if type(issecretvalue) == "function" then
        print("start secret =", issecretvalue(startTime))
        print("dur secret   =", issecretvalue(duration))
        print("enable secret=", issecretvalue(enable))
    end
end

SLASH_RGBCD1 = "/rgbcd"

SlashCmdList["RGBCD"] = function()

    local startTime, duration, enable = GetActionCooldown(1)

    print("start=", tostring(startTime))
    print("duration=", tostring(duration))
    print("enable=", tostring(enable))
end

SLASH_RGBSLOT1 = "/rgbslot"

SlashCmdList["RGBSLOT"] = function(msg)

    local slot = tonumber(msg) or 1

    local actionType, id, subType = GetActionInfo(slot)

    print("slot", slot)
    print("type", tostring(actionType))
    print("id", tostring(id))
    print("subtype", tostring(subType))
end

SLASH_RGBPOS1 = "/rgbpos"

SlashCmdList["RGBPOS"] = function()

    local screen_width, screen_height = GetPhysicalScreenSize()
    local ui_width, ui_height = UIParent:GetSize()
    local x_scale = screen_width / ui_width
    local y_scale = screen_height / ui_height
    local ui_left = UIParent:GetLeft()
    local ui_top = UIParent:GetTop()
    local function ToScreenX(value)
        return math.floor((value - ui_left) * x_scale + 0.5)
    end
    local function ToScreenY(value)
        return math.floor((ui_top - value) * y_scale + 0.5)
    end

    local left = ToScreenX(holder:GetLeft())
    local right = ToScreenX(holder:GetRight())
    local top = ToScreenY(holder:GetTop())

    print(
        string.format(
            "RGB Strip: X-left=%dpx X-right=%dpx Y-top=%dpx Width=%dpx Height=%dpx Pixel-scale X=%.3f Y=%.3f",
            left,
            right,
            top,
            math.floor((BOX_COUNT + 4) * BOX_SIZE * x_scale + 0.5),
            math.floor(BOX_SIZE * y_scale + 0.5),
            x_scale,
            y_scale
        )
    )

    for slot = 1, BOX_COUNT + 4 do

        local label
        local box
        local box_number

        if slot <= 4 then
            label = "Signal"
            box = sigBoxes[slot]
            box_number = slot
        else
            label = "Box"
            box = boxes[slot - 4]
            box_number = slot - 4
        end

        if box then
            local y_top = ToScreenY(box:GetTop())
            local x_left = ToScreenX(box:GetLeft())
            print(
                string.format(
                    "%s %d: X-left=%dpx Y-top=%dpx",
                    label,
                    box_number,
                    x_left,
                    y_top
                )
            )
        end
    end

end
--------------------------------------------------
-- Event Frame
--------------------------------------------------

local frame = CreateFrame("Frame")

frame:RegisterEvent("PLAYER_LOGIN")

frame:SetScript("OnEvent", function()

    CreateBoxes()

    print("[" .. addonName .. "] Loaded 0.1.4")

    frame:SetScript("OnUpdate", function(self, elapsed)

        self.timer = (self.timer or 0) + elapsed

        if self.timer < 0.25 then
            return
        end

        self.timer = 0

        UpdateBoxes()

    end)

end)