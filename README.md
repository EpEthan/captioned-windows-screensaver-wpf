# Windows Random Image Screensaver (WPF)

The default Windows slideshow screensaver isn't good enough:
1. It's randomization is deterministic (as long as you don't add photos the slideshow order is constant)
2. It doesn't animate the photos
3. It doesn't allow for much configuration

This is an improved version of that screensaver!

## Installation

1. Build the project
2. Change the `.exe` file name to `.scr`
3. Put the file in system32 (C:\Windows\System32)
4. Pick the screensaver in the dialog (Control Panel > Change Screensaver)

## Checklist
- [ ] go back and forward in photos feature
  - back should go to the actual last photo we were in
  - forward after hitting back should go to the actual photo we were in
  - forward when not in "history" should get a new photo
- [ ] text overlay: show image "source" (directory name)
  - animate in with picture
- [ ] bugfix: calling Close() before in view causes a crash (i.e. finished iterating over all folders before in view)

