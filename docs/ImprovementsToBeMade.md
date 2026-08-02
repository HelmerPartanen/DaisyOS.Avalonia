# Improvements to make. (Start from the most impactful and mark done with [X])

- [X] Quicksearch hotkey should be Ctrl+Space.
- [X] Quicksearch results should appear immediately without a resize or fade animation.

- [ ] Add a way to create multiple users. users must choose the user to log in on lock screen.

- [X] Launcher should always be the highest on z axis and close when it loses focus (for example i click another application) The launcher behaviour should be aligned with other operating system launcher behaviours

- [X] Wallpaper picker wallpaper previews currently not rounded/clipped (goes over the rounded container at the corners)

-[X] Detect if user has a bluetooth receiver/wifi receiver. If not dont display on the systembar by default.

- [X] Right click systembar to open a context menu with all the possible systembar items (wifi, bluetooth, game mode etc). Selected items using primary text color and check icon at the left. Unselected using secondary text color and transparent check icon.
Allow users to select/deselect multiple without closing the menu after just selecting one.

- [X] Change systembar text color to secondary, active items like wifi on bluetooth on using primary, wifi off bluetooth off = non active using tertiary text color.

- [X] Shell should display a human-readable system-drive name in File Manager and Settings instead of just a slash (/).

- [X] Media widget not updating song and artowrk in realtime or even at the same time. Media widget should update the media details as realtime as possible, and smoothly instead of just snapping while being lightweight.

- [X] Add setting for users to control media widget properties. Required settings are: Size, Compact (just the width of the artwork (just a square aspect ratio) that displays full details and media controls on hover), Basic (the current size that displays media control buttons on hover and title and artist name in default state), Large (Displays the media controls at alltimes). Next setting, Use normal color (glass) or adaptive tint, meaning that the average color of artwork / favicon is extracted and used as a tint color with 50% opacity in the media player that sits above the normal material so the normal material automatically changes the dark and light values based of system theme. Media controls should be still accessable.

- [X] Clock, user must be able to choose to change system time and timezone in settings and also choose if the systembar clock displays only time or also date. Date and time ordering follows the selected system region.

- [ ] Audio menu and audio settings should check for real devices and allow users to change the device, In audio settings users must be able to see the device properties for example change the sampling rates and bit depths. Ofcourse only displaying what the device supports. Also to see available output and input devices and change them.

- [ ] Custom audio EQ and importing universal EQ profiles (in the future)

- [ ] In display settings users should be able to change resolution, refresh rate, enable hdr, change the color profile / import a color profile (.icc/icm).

- [ ] Multiple monitor support (configurable in settings)

- [ ] Multiple password option, password, passcode (series of numbers that unlocks the device on the correct passcode without user having to press enter), fingerprint if possible, no password (always opens straight into desktop).

- [ ] Users should be able to change the position of the whole bottom components (media player, dock, systembar). Currently only dock is movable but instead of just the dock, it should be everything so there is not one thing at the left and other thing at the bottom.

- [ ] Instead of using rigid animation, use a light spring animations to make everything more alive.

- [ ] File manager improvements. The current file manager UI is OK, not perfect. The header input fields should be same height as the header buttons, The path input area should work same way as windows 11, The paths should be clikable but also allow users to write/copy/paste/open in a new window/new tab, if user clicks empty area in the input box auto highlight the path for easy copy. Also overall layout should feel easy to use and familiar without weird positions/layouts.

- [X] Organize the systembar into more smart order and instead of displaying battery precent as a icon, Display it as % (example: 48%). 21-100% using the secndary text color, 10-20% using danger text color. Charging state should use green swatch color with a charging icon at the left. When pluggin in charger, the icon should appear smoothly without weird layout pops in the system bar.

- [ ] Live wallpaper support in the future. The default wallpapers hopefully will be live wallpapers that plays the wallpaper video on startup/wake up and slows smoothly down to display a still image. Taking about 5 seconds to stop for a still image.

- [ ] Make sure that users are able to use wallpaper engine/other wallpaper application to use custom live wallpapers without any hiccups.

- [ ] If user updates the DaisyOS operating system and the new update has bugs or something. User must be able to rollback to the previous update to get a working system and update again until a patch is made.
- [ ] Low power mode and power management settings (Sleep mode timer off-30min, turn off display after a time period, Performance mode, balanced or Eco modes)

- [X] Remove updates panel since settings already has an update section.
