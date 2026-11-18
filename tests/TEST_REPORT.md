# CityFix — test report

Run: 2026-11-19 00:08. Result: **451 / 451 passed**, 0 failed, 0 skipped.

Run: `dotnet test tests/CityFix_backend.Tests` (layer only: `--filter Category=Unit|Integration|Functional|E2E|Architecture|Performance`).

| Layer | Methods | Runs (with Theory cases) | Passed |
|---|---|---|---|
| Unit | 111 | 185 | 185 |
| Integration | 122 | 124 | 124 |
| Functional | 63 | 99 | 99 |
| E2E | 9 | 9 | 9 |
| Architecture | 18 | 28 | 28 |
| Performance | 6 | 6 | 6 |
| **Total** | 329 | 451 | 451 |

## Unit — pure logic without a DB or network

### AppealOptionsTests

- ✅ Configured deadlines are used
- ✅ Defaults match the documented values

### AppealPolicyTests

- ✅ Author edits only while new or waiting for info
- ✅ Heads process only their association
- ✅ Neighbours can view but not change an appeal
- ✅ Workflow allows only listed transitions _(5 cases)_

### AppealVisibilityTests

- ✅ Administrator sees everything
- ✅ Any signed in user may open any appeal card
- ✅ Denied actions throw forbidden
- ✅ Head can assign only in own association
- ✅ Head sees the association and own appeals
- ✅ Head without association sees only own appeals
- ✅ Rating window is inclusive of the last day
- ✅ Resident sees only own appeals in lists

### AppealWorkflowTests

- ✅ Every open status can still reach a closed one
- ✅ Final statuses are exactly the closed ones _(8 cases)_
- ✅ Next statuses agree with CanMove and never return to New _(8 cases)_
- ✅ Next statuses follow the display order
- ✅ Open and closed statuses split every status exactly once
- ✅ Staying in the same status is always allowed _(8 cases)_

### EmailTemplateTests

- ✅ Greeting uses the name when known
- ✅ Html declares the reader language
- ✅ Html version encodes everything that came from users
- ✅ Letter without a link has no button
- ✅ Text version has all parts and the plain link

### FeedbackAndDeadlineTests

- ✅ Deadline follows the priority _(3 cases)_
- ✅ Neighbours support open appeals but not their own
- ✅ Only open appeals past the deadline are overdue
- ✅ Only the author rates a done appeal once and in time
- ✅ Reopening is limited

### FileValidationTests

- ✅ Empty and oversized files are rejected
- ✅ Gif is recognised by its signature
- ✅ Photo count includes already attached photos
- ✅ Real images are accepted _(3 cases)_
- ✅ Riff files that are not webp are rejected
- ✅ Stored paths cannot escape the upload folder _(3 cases)_
- ✅ Stored paths resolve inside the configured folder
- ✅ Type is decided by content not by name

### GeocodingClientTests

- ✅ Answers are cached per point and language
- ✅ Reverse builds a short address from the parts
- ✅ Reverse falls back to the display name
- ✅ Search is limited to the city and parses coordinates
- ✅ Service failures give no address
- ✅ Too short or unknown searches return nothing

### HelperTests

- ✅ Csv cells cannot become formulas _(6 cases)_
- ✅ Csv keeps negative numbers as numbers
- ✅ Emails are compared case insensitively
- ✅ Invitation codes ignore spaces and case
- ✅ Phone numbers are normalised to E164 _(5 cases)_
- ✅ Search pattern escapes like wildcards

### ImageMetadataTests

- ✅ Jpeg drops exif comments and trailer but keeps orientation and image data
- ✅ Jpeg without rotation gets no exif at all
- ✅ Png drops text and exif chunks
- ✅ Truncated jpeg is rejected
- ✅ Webp drops exif and xmp and fixes header

### LocalizedTests

- ✅ Appeal shows the translation only to readers of the other language
- ✅ Full name skips an empty last name
- ✅ Pick prefers the current language and falls back to the other
- ✅ Ukrainian names are shown transliterated in english
- ✅ Untranslated appeal shows the original

### MvcFilterTests

- ✅ Forbidden actions become 403 for signed in users and a login for guests
- ✅ Missing records become 404
- ✅ Normal users are not redirected
- ✅ Other exceptions are left to the error page
- ✅ Password change pages stay reachable _(5 cases)_
- ✅ Users with a temporary password are sent to the profile

### NotificationTextTests

- ✅ Assignment uses the responsible name in the reader language
- ✅ Every type has its own title
- ✅ Rating notification shows the stars
- ✅ Status change names the appeal and the new status
- ✅ Supporters get no comment meant for the author

### PagingAndFilterTests

- ✅ Active filter count and route values reflect the filter
- ✅ Page number is clamped to existing pages _(5 cases)_
- ✅ Page reports its range and maps items
- ✅ Responsible filter understands association and recipient ids
- ✅ Unknown tabs fall back to all _(4 cases)_

### ReportsTests

- ✅ Analytics reports render in both formats
- ✅ Appeal pdf is a multi page document
- ✅ Appeal spreadsheet has the header and one row per appeal
- ✅ Empty report still renders
- ✅ File names use kyiv time

### SecurityPrimitiveTests

- ✅ Corrupted hash is a failed login not a crash
- ✅ Generated codes avoid look alike characters
- ✅ International numbers keep their country code _(5 cases)_
- ✅ Null email normalises to empty
- ✅ Password hash verifies only the right password
- ✅ Same password gets a different salt each time
- ✅ Security stamps are unique and fit the column

### TextLanguageAndTransliterationTests

- ✅ Language is detected by the majority alphabet _(6 cases)_
- ✅ Names are transliterated by the national standard _(9 cases)_
- ✅ Other language flips uk and en

### ThumbnailTests

- ✅ Exif orientation is applied
- ✅ Large photos shrink to the maximum side keeping proportions
- ✅ Small photos are not enlarged
- ✅ Undecodable data gives no thumbnail

### TranslatorClientTests

- ✅ DeepL errors become http exceptions
- ✅ DeepL uses the free host for free keys and sends the key _(2 cases)_
- ✅ MyMemory accepts status sent as a string
- ✅ MyMemory quota warnings and errors are failures _(3 cases)_
- ✅ MyMemory splits long text into chunks within the byte limit

### UiFormatTests

- ✅ Dates and coordinates use a fixed format
- ✅ Kyiv day start is converted to utc
- ✅ Kyiv time follows winter and summer offsets
- ✅ Time ago switches to a date after a week

### ViewModelValidationTests

- ✅ Appeal coordinates must be on the globe and category and responsible are required
- ✅ Appeal form maps the responsible choice _(3 cases)_
- ✅ Appeal form round trips an appeal
- ✅ Appeal title needs at least five characters _(2 cases)_
- ✅ Password change requires confirmation
- ✅ Registration requires a long enough matching password and a real email
- ✅ Valid registration passes

## Integration — services + real PostgreSQL/PostGIS

### AcabServiceTests

- ✅ Associations in use cannot be deleted
- ✅ Codes are found whatever the spacing or case
- ✅ Head code can be consumed only once even concurrently
- ✅ List counts members heads and appeals
- ✅ New association gets two different invitation codes
- ✅ Regenerated code replaces the old one
- ✅ Update trims and clears blank fields

### AdminSeederTests

- ✅ Development generates a password when none is configured
- ✅ Existing resident with the admin email is not promoted
- ✅ First start creates one administrator who must change the password
- ✅ Production refuses to invent an administrator password

### AnalyticsServiceTests

- ✅ Dashboard kpis count active closed and response time
- ✅ Empty scope gives empty figures instead of errors
- ✅ Head analytics cover only the association
- ✅ Management figures reflect deadlines ratings and hot spots

### AppealServiceTests

- ✅ Appeal card shows similar appeals and what the viewer may do
- ✅ Appeal needs a point an existing category and a valid responsible
- ✅ Author edits while new but not after work started
- ✅ Author rates a done appeal once and heads hear about it
- ✅ Authors staff and closed appeals cannot be supported
- ✅ Closing as done needs a comment and empty changes are refused
- ✅ Comment without status change reaches the author
- ✅ Concurrent processing of the same appeal lets only one win
- ✅ Deleting removes the appeal and its photos
- ✅ Disallowed transitions and stale forms are rejected
- ✅ Editing only the location keeps the translation
- ✅ English text is detected as english
- ✅ Export returns readable rows for staff
- ✅ Head changing the responsible while editing records the assignment
- ✅ Heads of the association are notified about a new appeal
- ✅ Invalid photo rejects the whole appeal
- ✅ List flags newer appeals at the same place as possible duplicates
- ✅ List respects visibility tabs search and filters
- ✅ Missing address is filled by reverse geocoding
- ✅ Missing thumbnails are rebuilt for old photos
- ✅ Neighbours toggle support and get status updates
- ✅ New appeal is saved with a deadline and queued for translation
- ✅ Only heads of the association and administrators process
- ✅ Others cannot rate and late ratings are refused
- ✅ Overdue appeals are reported to heads once
- ✅ Photo limit counts photos already attached
- ✅ Photos are stored without metadata and get thumbnails
- ✅ Processing follows the workflow and writes history
- ✅ Raising the priority moves the deadline from the original start
- ✅ Rating must be between one and five _(2 cases)_
- ✅ Reassigning notifies the author and refuses closed or unchanged appeals
- ✅ Reopening needs a reason and is limited
- ✅ Reopening restarts the work with a new deadline
- ✅ Residents without association send appeals to city services
- ✅ Similar appeals are found nearby in the same category
- ✅ Single photo can be removed by the editor only
- ✅ Stats split open and closed appeals visible to the actor
- ✅ Support count stays right under parallel clicks
- ✅ Without a head administrators are notified instead

### AuditLogTests

- ✅ Entries record who did what from where
- ✅ Search matches action target or details and treats wildcards literally

### AuthServiceTests

- ✅ Account exists letter contains a reset link
- ✅ Administrators cannot use association codes
- ✅ Blocked users cannot sign in
- ✅ Changing the password requires the current one
- ✅ Confirmation letters are throttled per address
- ✅ Confirmation token confirms the email once
- ✅ Confirming a pending email switches the address
- ✅ Cookie refresh keeps valid sessions updates changed ones and drops stale ones
- ✅ Five wrong passwords lock the account for fifteen minutes
- ✅ Forged tokens are rejected _(2 cases)_
- ✅ Head code makes a head and works only once
- ✅ Head stepping down stays in the association as a resident
- ✅ Login works with any email case and the right password
- ✅ Password reset token works once and signs out other sessions
- ✅ Pending email taken meanwhile is reported
- ✅ Principal carries identity role and stamp
- ✅ Registering a confirmed email again does not reveal or duplicate the account
- ✅ Registering again replaces an unconfirmed account
- ✅ Registration without a code creates an unconfirmed resident
- ✅ Reset tokens are not issued or accepted for blocked users
- ✅ Resident code joins the association as a resident
- ✅ Residents join and leave associations with codes
- ✅ Successful login resets the failure counter
- ✅ Token for an old address does not confirm the new one
- ✅ Unknown code is reported on the code field

### BackgroundWorkerTests

- ✅ Deadline worker reports overdue appeals on start
- ✅ Disabled translation does nothing
- ✅ Dispatcher emails pending notifications in the users language
- ✅ Dispatcher gives up after the last attempt
- ✅ Dispatcher schedules a retry after a failure
- ✅ Dispatcher skips blocked and opted out users
- ✅ Translator fills in appeals names and addresses
- ✅ Translator stops retrying an appeal after the attempt limit

### DatabaseSchemaTests

- ✅ All migrations apply to an empty database
- ✅ Deleting an appeal cascades to its history files supports and notifications
- ✅ Duplicate emails are refused by the database
- ✅ Every entity has its table
- ✅ Location is computed from the coordinates
- ✅ Model has no changes missing from the migrations
- ✅ PostGIS is installed
- ✅ Spatial and lookup indexes exist
- ✅ Users with appeals cannot be deleted at database level

### NotificationServiceTests

- ✅ Actor is never notified about own action
- ✅ Cleanup removes old notifications but keeps undelivered mail
- ✅ Inbox shows only own in app notifications newest first
- ✅ Recipients preferences decide the channels
- ✅ Users mark only their own notifications read

### UserServiceTests

- ✅ Admin email change is applied immediately and resets the translated name
- ✅ Admin reset issues a new password and ends sessions
- ✅ Administrator creates users with a one time password
- ✅ Administrator searches users by name or email
- ✅ Administrators cannot block themselves
- ✅ Blocked or missing actor is rejected
- ✅ Changing own email waits for confirmation
- ✅ Emails are unique regardless of case
- ✅ Head lists only members of own association
- ✅ Head must belong to an association and the association must exist
- ✅ Notification settings and language are saved
- ✅ Only administrators manage users
- ✅ Personal data export contains profile appeals history and notifications
- ✅ Profile cannot take someone elses email
- ✅ Profile statistics count own appeals supports and unread notifications
- ✅ Profiles are visible to self own head and administrators only
- ✅ Self deletion anonymises the account and keeps appeals
- ✅ The last active administrator cannot be removed
- ✅ Users with appeals or history are kept others can be deleted

## Functional (HTTP) — the whole application in memory via WebApplicationFactory

### AccountFlowTests

- ✅ Account deletion needs the password and anonymises the user
- ✅ Auth cookie is http only lax and persistent only when asked
- ✅ Blocking a user ends the open session
- ✅ Changing the password signs out other browsers
- ✅ Forgotten password is reset through the emailed link once
- ✅ Login returns only to local pages _(3 cases)_
- ✅ Logout ends the session
- ✅ Personal data can be downloaded
- ✅ Profile email change is confirmed through the new address
- ✅ Registering with a taken email looks the same but mails the owner
- ✅ Registration form errors are shown and nothing is saved
- ✅ Repeated failures lock the account
- ✅ Residents join an association with its code
- ✅ Seeded administrator must replace the temporary password first
- ✅ Unknown email gets the same answer and no letter
- ✅ Visitor registers confirms the email and signs in
- ✅ Wrong current password does not change anything
- ✅ Wrong password keeps the visitor on the login page

### AdminHttpTests

- ✅ Administrator blocks resets and deletes users
- ✅ Administrator manages categories and every change is audited
- ✅ Analytics reports download _(2 cases)_
- ✅ Category with appeals cannot be deleted
- ✅ Created user signs in with the shown password and must change it
- ✅ Empty category name is rejected
- ✅ Head sees residents of the association but cannot edit them
- ✅ New association hands out codes that let people register
- ✅ Recipients are managed like categories

### AppealHttpTests

- ✅ Address lookup uses the geocoder
- ✅ Author edits and deletes a new appeal
- ✅ Csv export is excel friendly and formula safe
- ✅ Head deleting a residents appeal is audited
- ✅ Head export contains only the association
- ✅ Head moves the appeal forward and the author is told
- ✅ Invalid appeal form is shown again with errors
- ✅ Invalid status change is reported and the form values kept
- ✅ Missing appeals give 404
- ✅ Notifications open the appeal and belong to their owner
- ✅ Photos are served privately without metadata
- ✅ Process ignores a foreign return url
- ✅ Rating and reopening work through the appeal card
- ✅ Resident files an appeal with a photo and the head gets an email
- ✅ Residents cannot process or edit other peoples appeals
- ✅ Similar appeals endpoint warns about duplicates nearby
- ✅ Staff export appeals in every format _(4 cases)_
- ✅ Support button toggles through json

### SiteSecurityTests

- ✅ Every main page renders for each role _(3 cases)_
- ✅ Forms without an antiforgery token are refused
- ✅ Guests are sent to the login page _(11 cases)_
- ✅ Health check reports a reachable database
- ✅ Landing page is public and sends security headers
- ✅ Language switch never redirects to another site
- ✅ Language switch sets a cookie and translates the site
- ✅ Navigation shows admin sections only to administrators
- ✅ Pages are open only to the right roles _(17 cases)_
- ✅ Signed in users switching language have it remembered
- ✅ Static files are served _(3 cases)_
- ✅ Token from another session is refused
- ✅ Too many sign in attempts from one address are throttled
- ✅ Unknown pages get a friendly 404
- ✅ Uploaded photos are not reachable as static files

### StartupConfigurationTests

- ✅ Production refuses to start without a public url
- ✅ Production refuses to start without smtp
- ✅ Production with full settings starts and hardens the responses

## E2E — real browser (headless Edge + Selenium) against Kestrel

### BrowserTests

- ✅ Empty or wrong login shows an error and does not sign in
- ✅ Head changes the status in the appeal card
- ✅ Landing page leads to the login form
- ✅ Language switch translates the interface
- ✅ Pages load without javascript errors or blocked resources
- ✅ Password eye button reveals the password
- ✅ Resident does not see staff menu or processing controls
- ✅ Resident picks a point on the map and files an appeal
- ✅ Visitor registers in the browser and signs in after confirming

## Architecture and localization — rules checked via reflection and source scanning

### ArchitectureRulesTests

- ✅ Account actions that change an account require sign in
- ✅ Appeal forms bind only whitelisted fields
- ✅ Controllers use service interfaces not implementations
- ✅ Entities with times store them as utc datetimes
- ✅ Every post action validates the antiforgery token
- ✅ Every service interface has exactly one implementation
- ✅ Lookup endpoints are rate limited
- ✅ Only public pages live in controllers without authorize
- ✅ Password and code guessing endpoints are rate limited _(8 cases)_
- ✅ Reference data and audit are for administrators only _(4 cases)_
- ✅ Services and models do not depend on the web layer
- ✅ User management writes are for administrators only

### LocalizationTests

- ✅ Both languages have the same keys
- ✅ English texts are actually translated
- ✅ Every display name and validation message exists
- ✅ Every key used in code and views exists
- ✅ No text is empty
- ✅ Translations use the same placeholders

## Performance / load — 2,500 appeals, parallel users

### LoadTests

- ✅ Analytics for the whole city render quickly
- ✅ Appeal lists stay fast with thousands of appeals
- ✅ Fifty parallel users are all served
- ✅ Full export of the association finishes in seconds
- ✅ Nearby search is fast and can use the spatial index
- ✅ Overdue scan handles a backlog in batches

