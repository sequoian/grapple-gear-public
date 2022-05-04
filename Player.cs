using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent (typeof (Controller2D))]
public class Player : MonoBehaviour 
{
	[Header("Running")]
	public float moveSpeed = 12;
	public float accelGrounded = .1f;
	public float decelGrounded = .1f;
	public float accelAirborne = .2f;
	public float decelAirborne = .2f;
	public float momentumDecelGrounded;
	public float momentumDecelAirborne;

	[Header("Jumping")]
	public float maxJumpHeight = 4.5f;
	public float minJumpHeight = .5f;
	public float timeToJumpApex = .5f;
	public float jumpGraceTime = .1f;
	public float jumpBufferTime = .08f;
	public float halfGravityThreshold = 1;

	[Header("Air Jumping")]
	public int maxAirJumps = 1;
	public float maxAirJumpHeight = 2;
	public float minAirJumpHeight = .5f;
	public float airJumpRefundTime;

	[Header("Wall Jumping")]
	public Vector2 wallJumpToward;
	public Vector2 wallJumpNeutral;
	public Vector2 wallJumpAway;
	public float wallStickTime = .15f;
	public float wallSlideMaxSpeed = 10;
	public float wallJumpCommitTime = .1f;
	public bool hurtleOnNeutral;

	[Header("Grappling")]
	public float grappleMaxLength = 10;
	public float grappleAngle = 45;
	public LayerMask grappleCollisionMask;
	public LayerMask grappleDingMask;
	public float grappleBufferTime = .08f;
	public float grappleCastRadius = 2;
	public float missPauseTime = .1f;
	public float extendTime = .15f;
	public float retractTime = .15f;

	[Header("Swinging")]
	public float swingSpeed;
	public float maxSwingSpeed;
	public float bonkVerticalThreshold;
	public float grappleStunTime = .15f;
	public float YMomentumModifier;

	[Header("Zipping")]
	public float zipSpeed;
	public float finalZipSpeed;
	public Vector2 finalZipDirRatio;

	[Header("Falling")]
	public float terminalVelocity = 20f;

	[Header("Death and Spawning")]
	public bool flipX;
	public LayerMask deathMask;
	public float deathTime;
	public float deathSkipTime;

	// Events
	public event Action deathEvent;
	public event Action nextRoomEvent;

	[HideInInspector]
	public Vector3 velocity;

	// Private variables
	Vector3 prevVelocity;
	Vector3 posPrev;
	Controller2D controller;
	StateMachine stateMachine;
	Vector2 input;
	Vector2 inputPrev;
	SpriteRenderer spriteRenderer;
	InputManager playerInput;
	Animator animator;
	PauseMenu pauseMenu;
	float gravity;
	float timeToWallUnstick;
	bool hurtling;
	bool onWall;
	bool onWallPrev;
	int wallDirX;
	AudioManager sounds;
	Vector3 initialPos;

	// Jump
	float maxJumpVelocity;
	float minJumpVelocity;
	float maxAirJumpVelocity;
	float minAirJumpVelocity;
	int airJumpCount;
	float jumpGraceTimer;
	float jumpBufferTimer;
	float wallJumpCommitTimer;
	bool jumping; // Set to false whenever moving upward without jumping
	float airJumpRefundTimer;
	bool airJumping;

	// Grapple
	Hook hook;
	Vector3 grapplePoint;
	float grappleLength;
	float grappleBufferTimer;
	float grappleCooldownTimer;
	float extendTimer;
	float retractTimer;
	float grapplePauseTimer;
	float grappleFacing;
	Vector3 hookPos;
	bool showHook;
	bool retractingAfterSwing;
	float longestDistance;
	GameObject grappledObject;
	bool hideArm;

	// Swing
	float swingAngle;
	int swingDirection;
	float finalSwingSpeed;
	bool isSpinning;

	// Other
	float deathTimer;
	float deathSkipTimer;
	bool skipDeathAnim;
	float bonkTimer;
	float lastSwingBounce;
	
	// States
	const int numStates = 6;
	const int normalState = 0;
	const int swingState = 1;
	const int deathState = 2;
	const int bonkState = 3;
	const int goalReachedState = 4;
	const int zipState = 5;

	void Start() 
	{
		// Get references to components
		controller = GetComponent<Controller2D>();
		spriteRenderer = GetComponent<SpriteRenderer>();
		hook = GetComponentInChildren<Hook>();
		sounds = FindObjectOfType<AudioManager>();
		playerInput = FindObjectOfType<InputManager>();
		animator = GetComponent<Animator>();
		pauseMenu = FindObjectOfType<PauseMenu>();

		// Subscribe to events
		FindObjectOfType<Room>().resetPlayer += OnRespawn;

		// Init state machine
		stateMachine = new StateMachine(numStates);
		stateMachine.AddState(normalState, NormalUpdate, NormalLateUpdate, null, null);
		stateMachine.AddState(swingState, SwingUpdate, SwingLateUpdate, SwingBegin, SwingEnd);
		stateMachine.AddState(deathState, DeathUpdate, null, DeathBegin, DeathEnd);
		stateMachine.AddState(bonkState, BonkUpdate, null, BonkBegin, null);
		stateMachine.AddState(goalReachedState, null, null, GoalReachedBegin, null);
		stateMachine.AddState(zipState, ZipUpdate, ZipLateUpdate, ZipBegin, ZipEnd);

		// Set initial state
		stateMachine.SetState(normalState);
		initialPos = transform.position;
		OnRespawn();

		// Calculate gravity and jump velocity by using jumpHeight, gravity and timeToJumpApex
		gravity = -(2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2);
		maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
		minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
		maxAirJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * maxAirJumpHeight);
		minAirJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minAirJumpHeight);
	}

	void Update()
	{
		if (pauseMenu.IsPaused())
		{
			return;
		}

		// Record previous velocity
		prevVelocity = velocity;

		// Get movement input
		inputPrev = input;
		input = playerInput.GetAxis("Move");

		// Get jump input and set timer
		if (playerInput.GetButtonDown("Jump"))
		{
			jumpBufferTimer = jumpBufferTime;
		}
		else if (playerInput.GetButtonUp("Jump"))
		{
			jumpBufferTimer = 0;
			airJumpRefundTimer = 0;
		}
		else
		{
			jumpBufferTimer -= Time.deltaTime;
			airJumpRefundTimer -= Time.deltaTime;
		}

		// Get grapple input and set timer
		if (playerInput.GetButtonDown("Grapple"))
		{
			grappleBufferTimer = grappleBufferTime;
		}
		else if (playerInput.GetButtonUp("Grapple"))
		{
			grappleBufferTimer = 0;
			extendTimer = 0;
			if (retractTimer == retractTime)
			{
				// Grapple aborted early, so retract time needs to be slower
				retractTimer = retractTime * (longestDistance / grappleMaxLength);
			}
		}
		else
		{
			grappleBufferTimer -= Time.deltaTime;
		}

		// Update grapple cooldown timer
		grappleCooldownTimer -= Time.deltaTime;

		// Update the current state
		stateMachine.Update();

		// Move player
		controller.Move((prevVelocity + velocity) * 0.5f * Time.deltaTime);

		// Late update for the current state
		stateMachine.LateUpdate();

		// Record previous position
		posPrev = transform.position;

		// Set sprite flip
		spriteRenderer.flipX = controller.faceDirection == 1 ? false : true;

		// Animations must be changed in normal update
		HandleAnimations();
	}

	void LateUpdate()
	{
		// Render grapple hook
		if (showHook)
		{
			int currentState = stateMachine.GetState();
			hook.DrawHook(hookPos, currentState == swingState || currentState == bonkState);
			hook.DrawLine(transform.position, hookPos);	
		}
		else
		{
			hook.HideHook(hookPos);
			hook.DrawLine(Vector2.zero, Vector2.zero);	
		}

		if (showHook && !hideArm)
		{
			hook.DrawArm(
				(int)grappleFacing, GameMath.AngleBetweenPoints(transform.position, hookPos));
		}
		else
		{
			hook.HideArm();
		}
	}

	// Normal State

	void NormalUpdate() 
	{
		wallJumpCommitTimer -= Time.deltaTime;

		// Jump
		bool wallJumping = false;
		if ((jumpBufferTimer > 0 || airJumpRefundTimer > 0) && (onWall || 
			(!controller.collisions.below && controller.checkNearbyWall())))
		{
			// Wall jump
			hurtling = false;
			int wallDirection = onWall ? wallDirX : controller.nearbyWallDirection;
			if (input.x == 0)
			{
				// Neutral wall jump
				velocity.y = wallJumpNeutral.y;

				if (hurtleOnNeutral)
				{
					velocity.x = -wallDirection * moveSpeed;
					hurtling = true;
				}
				else
				{
					velocity.x = -wallDirection * wallJumpNeutral.x;
				}
			}
			else if (Mathf.Sign(input.x) == Mathf.Sign(wallDirection))
			{
				// Jump while pushing against wall
				velocity.x = -wallDirection * wallJumpToward.x;
				velocity.y = wallJumpToward.y;
				hurtling = false;
			}
			else
			{
				// Jump while pushing away from wall
				velocity.x = -wallDirection * wallJumpAway.x;
				velocity.y = wallJumpAway.y;
				hurtling = false;
			}

			// Air jump refund
			if (airJumpRefundTimer > 0)
			{
				airJumpRefundTimer = 0;
				airJumpCount = maxAirJumps;
				airJumping = false;
			}

			sounds.PlayJump();
			jumpBufferTimer = 0;
			wallJumping = true;
			jumping = true;

			wallJumpCommitTimer = wallJumpCommitTime;
		}
		else if (jumpBufferTimer > 0 && jumpGraceTimer > 0) 
		{
			// Jump from ground
			sounds.PlayJump();
			velocity.y = maxJumpVelocity;
			jumpGraceTimer = 0;
			jumpBufferTimer = 0;
			jumping = true;
		}
		else if (jumpBufferTimer > 0 && airJumpCount > 0)
		{
			// Air jump
			velocity.y = maxAirJumpVelocity;
			jumpBufferTimer = 0;
			airJumpCount -= 1;
			jumping = true;
			airJumpRefundTimer = airJumpRefundTime;
			airJumping = true;
		}
		else if (playerInput.GetButtonUp("Jump") && velocity.y > minJumpVelocity && jumping)
		{
			// Variable jump height
			// Reduce the height of the jump if releasing the jump button
			velocity.y = airJumpCount < maxAirJumps ? minAirJumpVelocity : minJumpVelocity;
		}

		// Play air jump sound if it wasn't refunded
		if (airJumping && airJumpRefundTimer <= 0)
		{
			sounds.PlayAirJump();
			airJumping = false;
		}

		// Disable hurtling if conditions are not met
		bool hurtleCollisions = controller.collisions.below ||
			controller.collisions.left || controller.collisions.right;
		bool movingOpposite = input.x != 0 && Mathf.Sign(input.x) == -Mathf.Sign(velocity.x);
		bool slowingDown = (input.x == 0 || Mathf.Sign(input.x) != Mathf.Sign(velocity.x)) && 
			(inputPrev.x != 0 && Mathf.Sign(inputPrev.x) == Mathf.Sign(velocity.x));
		if ((hurtleCollisions || movingOpposite || slowingDown) && !wallJumping)
		{
			hurtling = false;
		}

		// Calulate horizontal acceleration. Force it to be either 0 or 1;
		float targetVelocityX = (Mathf.Abs(input.x) > 0 ? 1 : 0) * Mathf.Sign(input.x) * moveSpeed;
		float accelerationX = 0;

		bool movingTooFast = Mathf.Sign(input.x) == Mathf.Sign(velocity.x) && 
			Mathf.Abs(velocity.x) > moveSpeed;
		if (movingTooFast && !hurtling && 
			input.x != 0 && Mathf.Sign(input.x) == Mathf.Sign(velocity.x))
		{
			// Decelerate slower when moving too fast and input is in that direction
			// Preserves momentum when going fast
			accelerationX = moveSpeed / 
				(controller.collisions.below ? momentumDecelGrounded : momentumDecelAirborne);
		}
		else if (((input.x == 0 || movingTooFast) && !hurtling) || 
			(wallJumpCommitTimer > 0 && !hurtling))
		{
			// Decelerate
			accelerationX = moveSpeed / 
				(controller.collisions.below ? decelGrounded : decelAirborne);
		}
		else if (!hurtling || (input.x != 0 && Mathf.Abs(velocity.x) < moveSpeed))
		{
			// Accelerate
			accelerationX = moveSpeed / 
				(controller.collisions.below ? accelGrounded : accelAirborne);
		}

		// Approach the target velocity from the current velocity using acceleration
		velocity.x = GameMath.Approach(velocity.x, targetVelocityX, accelerationX * Time.deltaTime);

		// Sticky wall
		if (onWall)
		{
			// Run timer if moving away from wall, otherwise reset timer
			if (timeToWallUnstick > 0)
			{
				if (input.x != wallDirX && input.x != 0)
				{
					timeToWallUnstick -= Time.deltaTime;
				}
				else
				{
					timeToWallUnstick = wallStickTime;
				}
			}
			else
			{
				timeToWallUnstick = wallStickTime;
			}

			// Stick to wall if the timer has not run out
			if (timeToWallUnstick > 0 && !wallJumping && onWallPrev)
			{
				velocity.x = 0;
			}
		}
		
		// Half gravity at peak of jump
		float gravMultiplier = Mathf.Abs(velocity.y) < halfGravityThreshold
			&& playerInput.GetButton("Jump") ? 0.5f : 1f;

		// Apply gravity
		velocity.y += gravity * gravMultiplier * Time.deltaTime;

		// Cap vertical velocity
		if (onWall && velocity.y < -wallSlideMaxSpeed)
		{
			// Wall slide
			velocity.y = -wallSlideMaxSpeed;
		}
		else if (velocity.y < -terminalVelocity)
		{
			// Terminal velocity
			velocity.y = -terminalVelocity;
		}
	}

	void NormalLateUpdate()
	{
		// Handle horizontal collisions
		if (controller.collisions.left)
		{
			wallDirX = -1;
		}
		else if (controller.collisions.right)
		{
			wallDirX = 1;
		}
		else
		{
			wallDirX = 0;
		}

		onWallPrev = onWall;
		onWall = false;
		if (controller.collisions.left || controller.collisions.right) 
		{
			// Stop x velocity if player is touching a wall
			velocity.x = 0;
			
			if (!controller.collisions.below)
			{
				// Player is on wall if not touching the ground
				onWall = true;
			}

			// Remove wall jump commit
			wallJumpCommitTimer = 0;
		}

		// Handle vertical collisions
		if (controller.collisions.above || controller.collisions.below) 
		{
			velocity.y = 0;
		}

		if (controller.collisions.below) 
		{
			// Keep track of jump grace timer
			jumpGraceTimer = jumpGraceTime;

			// Reset air jumps
			airJumpCount = maxAirJumps;
		}
		else
		{
			jumpGraceTimer -= Time.deltaTime;
		}

		// Handle grappling
		if (retractingAfterSwing)
		{
			if (retractTimer > 0)
			{
				hideArm = true;

				retractTimer -= Time.deltaTime;

				float angle = Mathf.Atan2((transform.position.y - grapplePoint.y), 
					(transform.position.x - grapplePoint.x));

				float distance = Mathf.Lerp(0, (transform.position - grapplePoint).magnitude, 
					retractTimer / retractTime);
		
				hookPos = transform.position - new Vector3(
					Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
			}
			else
			{
				retractingAfterSwing = false;
				showHook = false;
				hideArm = false;
			}
		}
		else
		{
			UpdateGrappleHook();
		}
	}

	// Swing State

	void SwingBegin()
	{
		// Bonk immediately if the player is already on the wall
		if (wallDirX == swingDirection)
		{
			stateMachine.SetState(bonkState);
		}

		// Choose momentum as swing speed if it is higher than default speed
		float momentumX = Mathf.Max(velocity.x * swingDirection, 0);
		float momentumY = Mathf.Max(velocity.y * -YMomentumModifier, 0);
		float momentum = momentumX + momentumY;

		// Point swing in the right direction
		if (isSpinning)
		{
			Spinner spinner = grappledObject.GetComponent<Spinner>();
			int spinDirection = spinner.spinsClockwise ? -1 : 1;
			finalSwingSpeed = Mathf.Max(spinner.spinSpeed, momentum) * spinDirection;
			grapplePoint = hookPos = spinner.transform.position;
			grappleLength = (grapplePoint - transform.position).magnitude;
		}
		else
		{
			// Regular swing
			finalSwingSpeed = Mathf.Max(swingSpeed, momentum) * swingDirection;
		}

		// Calculate the angle between the grapple point and the player
		Vector3 position = transform.position;
		swingAngle = Mathf.Atan2((position.y - grapplePoint.y), (position.x - grapplePoint.x));

		controller.swinging = true;
		onWall = false;

		sounds.PlayGrappleHit();
	}

	void SwingUpdate()
	{
		// Calculate the angle the player should be this frame
		// Divide by grapple length to maintain a constant speed. That way, longer swings
		// won't make the player go faster.
		swingAngle += finalSwingSpeed / grappleLength * Time.deltaTime;

		float swingAngleDegrees = swingAngle * Mathf.Rad2Deg % 360;
		bool breakLimitReached = swingAngleDegrees < -180 || (swingAngleDegrees > 0 && 
			swingAngleDegrees < 180);
		if (!isSpinning && breakLimitReached)
		{
			// Cap swing angle
			swingAngle = swingAngleDegrees < -180 ? -180 * Mathf.Deg2Rad : 0;
		}

		// Break grapple if button is released
		if (!playerInput.GetButton("Grapple"))
		{
			stateMachine.SetState(normalState);
		}

		// Calcuate how far from the grapple point the player will be based on the swing angle
		Vector3 offset = new Vector3(Mathf.Cos(swingAngle), Mathf.Sin(swingAngle)) 
			* grappleLength;

		// Calculate where the player should be placed without collisions factored in
		Vector3 newPosition = grapplePoint + offset;

		// Calculate final velocity based on positions
		// Remove delta time from calculation
		velocity = (newPosition - transform.position) / Time.deltaTime;

		if (!isSpinning && breakLimitReached)
		{
			// If the grapple was broken at its limit, make sure that the velocity hides
			// the fact that the player didn't travel as far this frame
			// Assign full y velocity since it's breaking at the horizontal line
			velocity.y = Mathf.Abs(finalSwingSpeed);
			velocity.x = 0;

			// Leave state
			stateMachine.SetState(normalState);
		}

		// Set the final velocity magnitude in case the player is releasing grapple. Results in
		// a consistent release velocity
		velocity = velocity.normalized * Mathf.Abs(finalSwingSpeed);
	}

	void SwingLateUpdate()
	{
		// Recalculate grapple length on corner corrections
		if (controller.collisions.corrected)
		{
			grappleLength = (grapplePoint - transform.position).magnitude;
		}

		if (controller.collisions.below)
		{
			// Glide along the ground
			velocity.y = 0;
			grappleLength = (grapplePoint - transform.position).magnitude;
		}
		else if (controller.collisions.above)
		{
			// Bonk against the ceiling
			stateMachine.SetState(bonkState);
		}
		else if (controller.collisions.right || controller.collisions.left)
		{
			// Hit the wall
			if (Mathf.Abs(velocity.y) < swingSpeed * bonkVerticalThreshold)
			{
				// Bonk if player hit the wall at a mostly horizontal velocity
				stateMachine.SetState(bonkState);
				return;
			}
			else
			{
				// Stop swinging
				velocity.x = 0;
				stateMachine.SetState(normalState);
			}
		}
		
		if (isSpinning)
		{
			// Only face one direction while spinning
			controller.faceDirection = (int)Mathf.Sign(finalSwingSpeed);
			grappleFacing = Mathf.Sign(finalSwingSpeed);

			if (controller.collisions.Any())
			{
				stateMachine.SetState(bonkState);
				return;
			}
		}
	}

	void SwingEnd()
	{
		hurtling = true;
		retractTimer = retractTime;
		retractingAfterSwing = true;
		jumping = false;
		controller.swinging = false;
		// lastSwingBounce = Vector2.zero;

		// Player can air jump after each grapple
		airJumpCount = maxAirJumps;
	}

	// Zip State

	void ZipBegin()
	{
		// Calculate the angle between the grapple point and the player
		Vector3 position = transform.position;
		swingAngle = Mathf.Atan2((position.y - grapplePoint.y), (position.x - grapplePoint.x));
	}

	void ZipUpdate()
	{
		if (!playerInput.GetButton("Grapple"))
		{
			stateMachine.SetState(normalState);
		}
		else if (transform.position.y < grapplePoint.y)
		{
			float oldLength = grappleLength;

			// Reduce the grapple length
			grappleLength -= zipSpeed * Time.deltaTime;

			// Calcuate how far from the grapple point the player will be based on the swing angle
			Vector3 offset = new Vector3(Mathf.Cos(swingAngle), Mathf.Sin(swingAngle)) 
				* grappleLength;

			// Calculate where the player should be placed
			Vector3 newPosition = grapplePoint + offset;

			// Calculate final velocity based on positions, removing delta time from calculation
			velocity = (newPosition - transform.position) / Time.deltaTime;
		}
		else
		{
			// Player reach grapple point
			stateMachine.SetState(normalState);
		}
	}

	void ZipLateUpdate()
	{
		if (controller.collisions.Any())
		{
			stateMachine.SetState(bonkState);
		}
	}

	void ZipEnd()
	{
		Vector3 zipDir = new Vector3(finalZipDirRatio.x * swingDirection,
			finalZipDirRatio.y, 0).normalized;
		velocity = zipDir * finalZipSpeed;

		airJumpCount = maxAirJumps;
		retractTimer = retractTime;
		retractingAfterSwing = true;
		jumping = false;
	}

	// Bonk State

	void BonkBegin()
	{
		velocity = Vector3.zero;
		sounds.PlayBonk();
		bonkTimer = grappleStunTime;
		hurtling = false;
		hideArm = true;
	}

	void BonkUpdate()
	{
		// Player cannot do anything
		bonkTimer -= Time.deltaTime;
		if (bonkTimer <= 0)
		{
			stateMachine.SetState(normalState);
		}
	}

	// Death State

	void DeathBegin()
	{
		velocity = Vector3.zero;
		deathTimer = deathTime;
		deathSkipTimer = deathSkipTime;
		sounds.PlayDeath();
		spriteRenderer.sortingLayerName = "Effects";
		showHook = false;
		GetComponent<BoxCollider2D>().enabled = false;
		spriteRenderer.color = Color.black;
		skipDeathAnim = false;
	}

	void DeathUpdate()
	{
		if (playerInput.GetButtonDown("Jump") || playerInput.GetButtonDown("Grapple"))
		{
			skipDeathAnim = true;
		}

		deathTimer -= Time.deltaTime;
		deathSkipTimer -= Time.deltaTime;

		if (deathTimer <= 0 || (skipDeathAnim && deathSkipTimer <= 0))
		{
			if (deathEvent != null)
			{
				deathEvent();
			}
		}
	}

	void DeathEnd()
	{
		spriteRenderer.sortingLayerName = "Player";
		spriteRenderer.color = Color.white;
	}

	// Goal Reached State

	void GoalReachedBegin()
	{
		velocity = Vector3.zero;
		showHook = false;
		if (nextRoomEvent != null)
		{
			nextRoomEvent();
		}
	}

	// Helpers

	void UpdateGrappleHook()
	{
		// Determine if player can grapple
		float facing;
		if (input.x != 0)
		{
			facing = Mathf.Sign(input.x);
		}
		else if (onWall)
		{
			facing = -wallDirX;
		}
		else
		{
			facing = controller.faceDirection;
		}

		if (grappleBufferTimer > 0 && grappleCooldownTimer <= 0)
		{
			// Begin grappling
			extendTimer = extendTime;
			grapplePauseTimer = missPauseTime;
			retractTimer = retractTime;
			grappleCooldownTimer = extendTime + missPauseTime + retractTime;
			grappleBufferTimer = 0;
			hookPos = transform.position;
			sounds.PlayGrapple();
			grappleFacing = facing;
		}

		// Set the direction
		Vector2 direction;
		if (grappleFacing == 0)
		{
			direction = Vector2.up;
		}
		else
		{
			float angle = grappleAngle * Mathf.Deg2Rad;
			direction = new Vector2(Mathf.Cos(angle) * grappleFacing, Mathf.Sin(angle));
		}

		// Set the distance
		float distance;
		float castDirMod = 1;
		if (extendTimer > 0)
		{
			// Extend the hook
			extendTimer -= Time.deltaTime;
			distance = Mathf.Lerp(grappleMaxLength, 0, extendTimer / extendTime);
			longestDistance = distance;
		}
		else if (grapplePauseTimer > 0)
		{
			// Pause the hook
			grapplePauseTimer -= Time.deltaTime;
			distance = longestDistance;
		}
		else if (retractTimer > 0)
		{
			// Retract the hook
			retractTimer -= Time.deltaTime;
			distance = Mathf.Lerp(0, longestDistance, retractTimer / 
				(retractTime * longestDistance / grappleMaxLength));
			castDirMod = -1;
		}
		else
		{
			distance = 0;
		}

		if (distance > 0)
		{
			Vector3 target = transform.position + (Vector3)direction * distance;
			float castDistance = (target - hookPos).magnitude;

			// Circle cast
			RaycastHit2D hit = Physics2D.CircleCast(hookPos, grappleCastRadius, 
				direction * castDirMod, castDistance, grappleCollisionMask | grappleDingMask);
			
			if (hit)
			{
				bool isDingLayer = ((1 << hit.collider.gameObject.layer) 
					& grappleDingMask) != 0;
				if (hit.collider.gameObject.tag == "Launcher")
				{
					grapplePoint = hit.point;
					grappleLength = (grapplePoint - transform.position).magnitude;
					hookPos = grapplePoint;
					swingDirection = (int)grappleFacing;
					retractTimer = 0;
					extendTimer = 0;
					grapplePauseTimer = 0;

					stateMachine.SetState(zipState);
				}
				else if (hit.collider.gameObject.tag == "Spinner")
				{
					grappledObject = hit.collider.gameObject;
					isSpinning = true;
					swingDirection = (int)grappleFacing;

					retractTimer = 0;
					extendTimer = 0;
					grapplePauseTimer = 0;

					stateMachine.SetState(swingState);
				}
				else if (!isDingLayer)
				{
					// Swing
					grapplePoint = hit.point;
					grappleLength = (grapplePoint - transform.position).magnitude;
					hookPos = grapplePoint;
					retractTimer = 0;
					extendTimer = 0;
					grapplePauseTimer = 0;
					isSpinning = false;

					// Calculate swing direction
					swingDirection = (int)grappleFacing;

					hook.PlayParticles(hit.normal);
					
					// Enter swing state
					stateMachine.SetState(swingState);
				}
				else if (grapplePauseTimer > 0)
				{
					// Ding if going forward or pausing
					sounds.PlayDing();

					hookPos = hit.point;
					longestDistance = (hookPos - transform.position).magnitude;
					
					extendTimer = 0;
					grapplePauseTimer = 0;
					retractTimer = retractTime * (longestDistance / grappleMaxLength);
				}
				else
				{
					// Ignore ding layer on way back
					hookPos = transform.position + (Vector3)direction * distance;
				}			
			}
			else
			{
				hookPos = transform.position + (Vector3)direction * distance;
			}
		}
		else
		{
			hookPos = transform.position;
		}

		if (distance > 0)
		{
			showHook = true;
		}
		else
		{
			showHook = false;
		}
	}

	void HandleAnimations()
	{
		const float floatThreshold = 2;
		switch (stateMachine.GetState())
		{
			case swingState:
			case normalState:
				if (onWall)
				{
					animator.Play("wall_slide");
				}
				else if (!controller.collisions.below && Mathf.Abs(velocity.y) < floatThreshold)
				{
					if (showHook)
					{
						animator.Play("float_armless");
					}
					else 
					{
						animator.Play("float");
					}
				}
				else if (!controller.collisions.below && velocity.y > 0)
				{
					if (showHook)
					{
						animator.Play("jump_armless");
					}
					else 
					{
						animator.Play("jump");
					}
				}
				else if (!controller.collisions.below && velocity.y <= 0)
				{
					if (showHook)
					{
						animator.Play("fall_armless");
					}
					else 
					{
						animator.Play("fall");
					}
				}
				else if (Mathf.Abs(velocity.x) > 0)
				{
					if (showHook)
					{
						animator.Play("run_armless");
					}
					else
					{
						animator.Play("run");
					}
				}
				else
				{
					if (showHook)
					{
						animator.Play("idle_armless");
					}
					else
					{
						animator.Play("idle");
					}
				}
				break;
			case zipState:
				animator.Play("jump");
				break;
			case bonkState:
				animator.Play("bonk");
				break;
			case deathState:
				animator.Play("death");
				break;
			case goalReachedState:
				animator.Play("victory");
				break;
			default:
				break;
		}
	}

	void OnRespawn()
	{
		stateMachine.SetState(normalState);

		velocity = Vector3.zero;
		prevVelocity = Vector3.zero;

		hurtling = false;
		onWall = false;
		retractingAfterSwing = false;
		hookPos = initialPos;
		showHook = false;

		jumpGraceTimer = 0;
		jumpBufferTimer = 0;

		grappleCooldownTimer = 0;
		grappleBufferTimer = 0;

		extendTimer = 0;
		grapplePauseTimer = 0;
		retractTimer = 0;
		
		controller.faceDirection = flipX ? -1 : 1;
		transform.position = initialPos;

		GetComponent<BoxCollider2D>().enabled = true;	
	}

	public void Win()
	{
		stateMachine.SetState(goalReachedState);
	}

	public void Die()
	{
		if (stateMachine.GetState() != deathState)
		{
			stateMachine.SetState(deathState);
		}
	}

	public void Bounce(Vector2 impulse, bool isVertical, float swingBoost)
	{
		// Unstick from wall if sliding onto spring
		onWall = false;

		if (stateMachine.GetState() == swingState)
		{
			if (Time.time - lastSwingBounce > .1f)
			{
				// Increase the swing speed
				finalSwingSpeed += finalSwingSpeed > 0 ? swingBoost : -swingBoost;

				// Cap the speed
				finalSwingSpeed = Mathf.Min(
					Mathf.Abs(finalSwingSpeed), maxSwingSpeed) * Mathf.Sign(finalSwingSpeed);

				// Reverse direction
				finalSwingSpeed *= -1;

				// Make sure a player does bounce more than once on the same spring
				lastSwingBounce = Time.time;
			}
		}
		else
		{
			// Bounce in the direction of the spring
			if (isVertical)
			{
				// Do not override x velocity on vertical impulse
				velocity.y = impulse.y;
			}
			else
			{
				velocity = impulse;
			}

			// Reset player state
			jumping = false;
			airJumpCount = maxAirJumps;
			stateMachine.SetState(normalState);
		}
	}

	public void StopSwinging()
	{
		if (stateMachine.GetState() == swingState)
		{
			stateMachine.SetState(normalState);
		}
	}

	void OnDrawGizmos()
    {
		GetComponent<SpriteRenderer>().flipX = flipX;
    }
}
