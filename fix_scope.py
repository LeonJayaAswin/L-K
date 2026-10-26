with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerMovement2D.cs') as f:
    c = f.read()

old = '''        // Allow movement in air for better feel
        if (!IsGrounded)
        {
            float targetSpeed = moveInput * moveSpeed;
            bool reversing = !Mathf.Approximately(moveInput, 0f)
                && Mathf.Abs(velocity.x) > 0.01f
                && Mathf.Sign(targetSpeed) != Mathf.Sign(velocity.x);
            float rate = reversing ? turnSpeed : (Mathf.Approximately(moveInput, 0f) ? (deceleration * 0.5f) : (acceleration * 0.7f));
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, rate * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
            if (!Mathf.Approximately(moveInput, 0f)) body.WakeUp();
            return;
        }'''
new = '''        // Allow movement in air for better feel
        if (!IsGrounded)
        {
            float airTargetSpeed = moveInput * moveSpeed;
            bool airReversing = !Mathf.Approximately(moveInput, 0f)
                && Mathf.Abs(velocity.x) > 0.01f
                && Mathf.Sign(airTargetSpeed) != Mathf.Sign(velocity.x);
            float airRate = airReversing ? turnSpeed : (Mathf.Approximately(moveInput, 0f) ? (deceleration * 0.5f) : (acceleration * 0.7f));
            velocity.x = Mathf.MoveTowards(velocity.x, airTargetSpeed, airRate * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
            if (!Mathf.Approximately(moveInput, 0f)) body.WakeUp();
            return;
        }'''
if old in c:
    c2 = c.replace(old, new)
    with open(r'D:\Unity\l&k\L-K\Assets\Scripts\Player\PlayerMovement2D.cs','w') as f:
        f.write(c2)
    print('fixed')
else:
    print('no')
