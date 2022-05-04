using UnityEngine;

[RequireComponent (typeof (BoxCollider2D))]
public class Controller2D : RaycastController {

	public LayerMask deathMask;
	public CollisionInfo collisions;
	public float upwardCornerCorrection = 0.5f;
	public float sideCornerCorrection = 0.5f;
	public float nearbyWallDistance;

	[HideInInspector]
	public int faceDirection;

	[HideInInspector]
	public int nearbyWallDirection;

	[HideInInspector]
	public bool swinging;
	
	public override void Awake() 
	{
		base.Awake();
		faceDirection = 1;
	}

	public void Move(Vector3 velocity) 
	{
		// Prepare for collisions
		UpdateRaycastOrigins();
		collisions.Reset();

		if (velocity.x != 0)
		{
			// Set which direction the player is facing
			faceDirection = (int)Mathf.Sign(velocity.x);
		}

		// Detect and resolve collisions
		HorizontalCollisions(ref velocity);

		if (velocity.y != 0) {
			VerticalCollisions(ref velocity);
		}

		// Move player
		transform.Translate(velocity);
	}

	void HorizontalCollisions(ref Vector3 velocity) 
	{
		// Set the length and direction for the raycast
		float directionX = faceDirection;
		float rayLength = Mathf.Abs(velocity.x) + skinWidth;
		if (Mathf.Abs(velocity.x) < skinWidth)
		{
			// Set minimal ray length if not moving
			rayLength = skinWidth * 2;
		}

		// Detect Collisions
		float hitDistance = -1;
		for (int i = 0; i < horizontalRayCount; i ++) {
			// Set the origin of the ray
			Vector2 rayOrigin = (directionX == -1) ? 
				raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
			rayOrigin += Vector2.up * (horizontalRaySpacing * i);

			// Cast the ray in the direction of horizontal movement and detect collisions
			RaycastHit2D hit = Physics2D.Raycast(
				rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

			Debug.DrawRay(rayOrigin, Vector2.right * directionX * rayLength, Color.green);

			if (hit) {
				hitDistance = hit.distance;

				// Reduce the ray length for the following raycasts to only detect closer collisions
				rayLength = hit.distance;
			}
		}

		if (hitDistance >= 0)
		{
			bool corrected = false;
			// Adjust boxcast scale so it does not collide with objects next to it
			const float boxcastScale = 0.99f;
			const float sweepStep = 0.1f;
			float castLength = sideCornerCorrection;

			// Bottom correction
			if (swinging && !corrected && velocity.y >= 0)
			{
				for (int i = 1; i < sideCornerCorrection * 10; ++i)
				{
					// Move next boxcast upward
					Vector2 origin = new Vector2(
						transform.position.x, transform.position.y + velocity.y + sweepStep * i
					);

					// Box cast in direction of velocity
					Vector2 direction = Mathf.Sign(velocity.x) == 1 ? Vector2.right : Vector2.left;
					RaycastHit2D hit = Physics2D.BoxCast(
						origin, collider2d.size * boxcastScale, 0f, 
						direction, castLength, collisionMask | deathMask);

					if (!hit)
					{ 
						// Adjust velocity up so player ends up next to the object
						velocity.y = Mathf.Ceil((origin.y) * 10) / 10 - transform.position.y;
						corrected = true;
						break;
					}
				}
			}

			// Top correction
			if (swinging && !corrected && velocity.y <= 0)
			{
				for (int i = 1; i < sideCornerCorrection * 10; ++i)
				{
					// Move next boxcast downward
					Vector2 origin = new Vector2(
						transform.position.x, transform.position.y + velocity.y - sweepStep * i
					);

					// Box cast in direction of velocity
					Vector2 direction = Mathf.Sign(velocity.x) == 1 ? Vector2.right : Vector2.left;
					RaycastHit2D hit = Physics2D.BoxCast(
						origin, collider2d.size * boxcastScale, 0f, 
						direction, castLength, collisionMask | deathMask);

					if (!hit)
					{ 
						// Adjust velocity down so player ends up next to the object
						velocity.y = Mathf.Floor((origin.y) * 10) / 10 - transform.position.y;
						corrected = true;
						break;
					}
				}
			}

			if (!corrected)
			{
				// set the velocity to the length between both colliders to avoid overlap
				velocity.x = (hitDistance - skinWidth) * directionX;

				// Set collision info for which side had collided
				collisions.left = directionX == -1;
				collisions.right = directionX == 1;
			}

			collisions.corrected = corrected;
		}
	}

	void VerticalCollisions(ref Vector3 velocity) 
	{
		// Set the length and direction for the raycast
		float directionY = Mathf.Sign(velocity.y);
		float rayLength = Mathf.Abs(velocity.y) + skinWidth;

		// Detect Collisions
		float hitDistance = -1;
		for (int i = 0; i < verticalRayCount; i ++) {
			// Set the origin of the ray
			Vector2 rayOrigin = (directionY == -1) ? 
				raycastOrigins.bottomLeft : raycastOrigins.topLeft;
			rayOrigin += Vector2.right * (verticalRaySpacing * i + velocity.x);

			Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.green);

			// Add jump thru to layer maks
			LayerMask jumpThruCheck = collisionMask;
			if (velocity.y <= 0)
			{
				jumpThruCheck = collisionMask | LayerMask.GetMask("Jump Thru");
			}

			// Cast the ray in the direction of horizontal movement
			RaycastHit2D hit = Physics2D.Raycast(
				rayOrigin, Vector2.up * directionY, rayLength, jumpThruCheck);
			if (hit) {
				if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Jump Thru"))
				{
					if (hit.collider.bounds.max.y > hit.point.y)
					{
						// Ignore jump through if the cast hit below the top of platform
						continue;
					}
				}

				hitDistance = hit.distance;

				// Reduce the ray length for the following raycasts to only detect closer collisions
				rayLength = hit.distance;
			}
		}

		// Resolve collisions
		if (hitDistance >= 0)
		{
			bool corrected = false;

			// Upward corner correction
			// Note: This only works if the stage is completely aligned with the grid (make sure
			// the position of the grid and the tileset use integers)
			if (velocity.y > 0)
			{
				// Adjust boxcast scale so it does not collide with objects next to it
				const float boxcastScale = 0.99f;
				// How far to cast the box. Should be long enough to contact above colliders
				const float castLength = 0.5f;
				const float sweepStep = 0.1f;

				// Moving left or stationary
				if (velocity.x <= 0)
				{
					// Sweep boxcast left
					// If the boxcast does not collide, adjust player velocity 
					// so they avoid collision
					for (int i = 1; i < upwardCornerCorrection * 10; ++i)
					{
						Vector2 origin = new Vector2(
							transform.position.x + velocity.x - sweepStep * i, transform.position.y
						);

						RaycastHit2D hit = Physics2D.BoxCast(
							origin, collider2d.size * boxcastScale, 0f, 
							Vector2.up, castLength, collisionMask | deathMask);

						if (!hit)
						{ 
							corrected = true;
							// Adjust velocity so player ends up next to the object
							velocity.x = Mathf.Ceil((origin.x) * 10) / 10 - transform.position.x;
							break;
						}
					}
				}

				// Moving right or stationary
				if (velocity.x >= 0)
				{
					// Sweep boxcast right
					for (int i = 1; i < upwardCornerCorrection * 10; ++i)
					{
						Vector2 origin = new Vector2(
							transform.position.x + velocity.x + sweepStep * i, transform.position.y
						);
						
						RaycastHit2D hit = Physics2D.BoxCast(
							origin, collider2d.size * boxcastScale, 0f, 
							Vector2.up, castLength, collisionMask | deathMask);

						if (!hit)
						{
							corrected = true;
							// Adjust velocity so player ends up next to the object
							velocity.x = Mathf.Floor((origin.x) * 10) / 10 - transform.position.x;
							break;
						}
					}
				}
			}

			// Handle collision if there is no upward corner correction
			if (!corrected)
			{
				// set the velocity to the length between both colliders to avoid overlap
				velocity.y = (hitDistance - skinWidth) * directionY;

				// Set collision info for which side had collided
				collisions.below = directionY == -1;
				collisions.above = directionY == 1;
			}

			collisions.corrected = corrected;
		}
	}

	public bool checkNearbyWall() 
	{
		// Set the length and direction for the raycast
		int[] directions = new int[2] {1, -1};
		float rayLength = nearbyWallDistance;
		
		foreach (int directionX in directions)
		{
			// For each horizontal ray
			for (int i = 0; i < horizontalRayCount; i ++) {
				// Set the origin of the ray
				Vector2 rayOrigin = (directionX == -1) ? 
					raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
				rayOrigin += Vector2.up * (horizontalRaySpacing * i);

				// Cast the ray in the direction of horizontal movement and detect collisions
				RaycastHit2D hit = Physics2D.Raycast(
					rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

				if (hit) {
					nearbyWallDirection = directionX;
					return true;
				}
			}
		}
		return false;
	}

	// Represents which side of the collider was hit by a collision
	public struct CollisionInfo 
	{
		public bool above, below;
		public bool left, right;
		public bool corrected;

		// Sets all values to false
		public void Reset() 
		{
			above = below = false;
			left = right = false;
			corrected = false;
		}

		public bool Any()
		{
			return above || below || left || right;
		}
	}
}
