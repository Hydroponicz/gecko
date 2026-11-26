extends CharacterBody3D
## Third-person player: camera-relative movement, sprint, jump, and an over-the-shoulder
## camera driven by mouse or right stick.

@export_group("Movement")
@export var walk_speed := 4.0
@export var sprint_speed := 7.5
@export var acceleration := 30.0  ## m/s², how quickly you reach target speed
@export var turn_speed := 12.0  ## how quickly the body turns to face movement
@export var jump_velocity := 4.5

@export_group("Camera")
@export var mouse_sensitivity := 0.0025  ## radians per pixel
@export var stick_sensitivity := 3.0  ## radians per second at full tilt
@export var min_pitch_degrees := -70.0
@export var max_pitch_degrees := 50.0
@export var start_pitch_degrees := -15.0

@onready var _model: Node3D = $Model
@onready var _yaw: Node3D = $CameraYaw
@onready var _pitch: Node3D = $CameraYaw/CameraPitch
@onready var _spring_arm: SpringArm3D = $CameraYaw/CameraPitch/SpringArm3D

var _gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity")
var _sprint_toggled := false
var _was_on_floor := true


func _ready() -> void:
	_spring_arm.add_excluded_object(get_rid())
	_pitch.rotation.x = deg_to_rad(start_pitch_degrees)
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		var motion := event as InputEventMouseMotion
		_rotate_camera(-motion.relative.x * mouse_sensitivity, -motion.relative.y * mouse_sensitivity)
	elif event.is_action_pressed("pause"):
		if Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
			Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		else:
			Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	elif event is InputEventMouseButton and event.is_pressed() and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _physics_process(delta: float) -> void:
	# Right-stick camera
	var look := Input.get_vector("look_left", "look_right", "look_up", "look_down")
	if look != Vector2.ZERO:
		_rotate_camera(-look.x * stick_sensitivity * delta, -look.y * stick_sensitivity * delta)

	# Gravity and jumping
	if not is_on_floor():
		velocity.y -= _gravity * delta
	elif Input.is_action_just_pressed("jump"):
		velocity.y = jump_velocity

	# Movement relative to where the camera faces. Analog sticks keep their partial tilt.
	var move_input := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var camera_yaw := Basis(Vector3.UP, _yaw.global_rotation.y)
	var direction := camera_yaw * Vector3(move_input.x, 0.0, move_input.y)

	if Input.is_action_just_pressed("sprint_toggle"):
		_sprint_toggled = not _sprint_toggled
	if move_input.length() < 0.1:
		_sprint_toggled = false
	var sprinting := Input.is_action_pressed("sprint") or _sprint_toggled
	var speed := sprint_speed if sprinting else walk_speed

	var horizontal := Vector3(velocity.x, 0.0, velocity.z)
	horizontal = horizontal.move_toward(direction * speed, acceleration * delta)
	velocity.x = horizontal.x
	velocity.z = horizontal.z

	# Turn the body (not the camera) to face the movement direction.
	if direction.length() > 0.1:
		var target_yaw := atan2(-direction.x, -direction.z)
		_model.rotation.y = lerp_angle(_model.rotation.y, target_yaw, turn_speed * delta)

	var fall_speed := -velocity.y
	move_and_slide()

	# Small rumble on a hard landing.
	if is_on_floor() and not _was_on_floor and fall_speed > 6.0:
		Controller.rumble(0.3, 0.6, 0.15)
	_was_on_floor = is_on_floor()


func _rotate_camera(yaw_delta: float, pitch_delta: float) -> void:
	_yaw.rotate_y(yaw_delta)
	_pitch.rotation.x = clampf(
		_pitch.rotation.x + pitch_delta,
		deg_to_rad(min_pitch_degrees),
		deg_to_rad(max_pitch_degrees)
	)
