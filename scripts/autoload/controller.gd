extends Node
## Tracks which input device the player is using (for on-screen button prompts)
## and wraps the PS5 DualSense features Godot supports natively:
## lightbar color, rumble and motion sensors.
## Adaptive triggers and the touchpad are not supported by Godot itself (see docs/design/input.md).

signal input_mode_changed(using_gamepad: bool)

const DEFAULT_LIGHT := Color(1.0, 0.55, 0.1)  # amber

var using_gamepad := false
var active_device := 0
var light_color := DEFAULT_LIGHT


func _ready() -> void:
	Input.joy_connection_changed.connect(_on_joy_connection_changed)
	for device in Input.get_connected_joypads():
		_on_joy_connection_changed(device, true)


func _input(event: InputEvent) -> void:
	if event is InputEventJoypadButton:
		active_device = event.device
		_set_mode(true)
	elif event is InputEventJoypadMotion and absf((event as InputEventJoypadMotion).axis_value) > 0.5:
		active_device = event.device
		_set_mode(true)
	elif event is InputEventKey or event is InputEventMouseButton:
		_set_mode(false)
	elif event is InputEventMouseMotion and (event as InputEventMouseMotion).relative.length() > 4.0:
		_set_mode(false)


## Sets the lightbar color on every connected controller that has one.
func set_light(color: Color) -> void:
	light_color = color
	for device in Input.get_connected_joypads():
		if Input.has_joy_light(device):
			Input.set_joy_light(device, color)


## weak/strong are 0..1 motor strengths, duration in seconds.
func rumble(weak: float, strong: float, duration: float) -> void:
	if Input.has_joy_vibration(active_device):
		Input.start_joy_vibration(active_device, weak, strong, duration)


func _set_mode(gamepad: bool) -> void:
	if gamepad != using_gamepad:
		using_gamepad = gamepad
		input_mode_changed.emit(gamepad)


func _on_joy_connection_changed(device: int, connected: bool) -> void:
	if connected and Input.has_joy_light(device):
		Input.set_joy_light(device, light_color)
