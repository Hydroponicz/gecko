extends Node
## Registers every input action in one place, for keyboard/mouse and gamepad.
## Gameplay code only ever asks for action names, never for specific keys or buttons.
##
## Godot uses Xbox-style button names. On a PS5 DualSense:
##   JOY_BUTTON_A = Cross, JOY_BUTTON_B = Circle, JOY_BUTTON_X = Square, JOY_BUTTON_Y = Triangle,
##   JOY_BUTTON_START = Options, JOY_BUTTON_LEFT_STICK = L3.

const DEADZONE := 0.2


func _enter_tree() -> void:
	# Movement
	_add("move_forward", [_key(KEY_W), _axis(JOY_AXIS_LEFT_Y, -1.0)])
	_add("move_back", [_key(KEY_S), _axis(JOY_AXIS_LEFT_Y, 1.0)])
	_add("move_left", [_key(KEY_A), _axis(JOY_AXIS_LEFT_X, -1.0)])
	_add("move_right", [_key(KEY_D), _axis(JOY_AXIS_LEFT_X, 1.0)])
	_add("jump", [_key(KEY_SPACE), _button(JOY_BUTTON_A)])  # Cross
	_add("sprint", [_key(KEY_SHIFT)])  # hold
	_add("sprint_toggle", [_button(JOY_BUTTON_LEFT_STICK)])  # L3, toggles until you stop moving

	# Camera (mouse look is handled directly in the player script)
	_add("look_left", [_axis(JOY_AXIS_RIGHT_X, -1.0)])
	_add("look_right", [_axis(JOY_AXIS_RIGHT_X, 1.0)])
	_add("look_up", [_axis(JOY_AXIS_RIGHT_Y, -1.0)])
	_add("look_down", [_axis(JOY_AXIS_RIGHT_Y, 1.0)])

	# General
	_add("pause", [_key(KEY_ESCAPE), _button(JOY_BUTTON_START)])  # Options

	# Debug
	_add("debug_lightbar", [_key(KEY_T), _button(JOY_BUTTON_Y)])  # Triangle


func _add(action: StringName, events: Array) -> void:
	if not InputMap.has_action(action):
		InputMap.add_action(action, DEADZONE)
	for event: InputEvent in events:
		InputMap.action_add_event(action, event)


func _key(keycode: Key) -> InputEventKey:
	var event := InputEventKey.new()
	event.device = -1
	event.physical_keycode = keycode
	return event


func _button(button: JoyButton) -> InputEventJoypadButton:
	var event := InputEventJoypadButton.new()
	event.device = -1
	event.button_index = button
	return event


func _axis(axis: JoyAxis, direction: float) -> InputEventJoypadMotion:
	var event := InputEventJoypadMotion.new()
	event.device = -1
	event.axis = axis
	event.axis_value = direction
	return event
