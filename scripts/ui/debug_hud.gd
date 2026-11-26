extends CanvasLayer
## Temporary on-screen info for testing input and controller features.

const TEST_COLORS: Array[Color] = [
	Color(1.0, 0.55, 0.1),  # amber
	Color(1.0, 0.0, 0.0),  # red
	Color(0.0, 0.3, 1.0),  # blue
	Color(0.0, 1.0, 0.3),  # green
	Color(1.0, 1.0, 1.0),  # white
]

var _color_index := 0

@onready var _label: Label = $Label


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("debug_lightbar"):
		_color_index = (_color_index + 1) % TEST_COLORS.size()
		Controller.set_light(TEST_COLORS[_color_index])
		Controller.rumble(0.5, 0.0, 0.1)


func _process(_delta: float) -> void:
	var lines := PackedStringArray()
	lines.append("Input: %s" % ("Gamepad" if Controller.using_gamepad else "Keyboard & Mouse"))
	for device in Input.get_connected_joypads():
		lines.append("Pad %d: %s | lightbar: %s  rumble: %s  motion: %s" % [
			device,
			Input.get_joy_name(device),
			Input.has_joy_light(device),
			Input.has_joy_vibration(device),
			Input.has_joy_motion_sensors(device),
		])
	lines.append("")
	lines.append("Move: WASD / Left stick    Look: Mouse / Right stick    Jump: Space / Cross")
	lines.append("Sprint: hold Shift / click L3    Release mouse: Esc / Options")
	lines.append("Test lightbar + rumble: T / Triangle")
	_label.text = "\n".join(lines)
