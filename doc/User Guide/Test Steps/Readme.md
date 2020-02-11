# Test Steps

## Basic Steps

Steps may be added to test plans which can have embedded steps themselves. Consider, for instance, a "sequential
step". This test step runs all of its child steps in sequence, and selects the most "severe" verdict of its child steps.
The verdict of the test plan is set based on the verdicts of its individual test steps, each of which generate their own
verdict.

Typically, but not always, verdicts are propagated upwards in the tree, prioritizing the most severe verdict. In other
words, a test plan outputs the "Fail" verdict if any of its immediate test steps fail, and likewise, a sequential test
step fails if any of its immediate children fail.

Like OpenTAP itself, test plans are designed for reuse, and minimizing the amount of work required of the user. For
example, it is possible to run a test plan with a range of values using the [sweep loop](todosweep_loop) test step. In
addition, many test step attributes can be marked as [external parameters](../cli%20usage/#external-settings), allowing
you to assign their values when the plan is loaded, instead of editing the plan itself. It is also possible to add a
[Test Plan Reference](todotest-plan-reference-link) as a test step, essentially allowing you to embed another test plan
within your plan. This is intended to minimize complexity, and encourage modular, self-contained test plans.

The way verdicts are propagated can of course be modified by plugins. For instance, a custom sequential step which
passes if any of its child steps pass can be implemented. This is covered in the [developer
guide](../../developer%20guide/test%20step).

#### Delay Step 

#### Dialog Step 

#### Run Program Step 

#### Time Guard Step

## Flow control
