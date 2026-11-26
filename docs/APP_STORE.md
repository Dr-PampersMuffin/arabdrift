# Q8 Drift (تقحيص): App Store submission

Bundle id `world.embur.arabdrift`, version 1.0.0, © 2026 Embur. Same approach as Sands of Empire (passed first time).

## Public pages (GitHub Pages, gh-pages branch, sources from `tools/site/build.py`)
- Privacy: https://dr-pampersmuffin.github.io/arabdrift/privacy.html
- Terms of Use (with Apple's Standard EULA): https://dr-pampersmuffin.github.io/arabdrift/terms.html
- Support: https://dr-pampersmuffin.github.io/arabdrift/support.html

These only work while the repo is public (free GitHub Pages). In the game: Settings (الإعدادات) has Privacy, Terms, Support and Restore Purchases; the pearl page has Terms, Privacy and Restore Purchases.

## Done by CI tags
| Tag | Does |
|---|---|
| `iap-N` | 4 pearl packs (consumable) + starter pack (non-consumable), EN/AR, prices, review screenshot |
| `preview-N` | renders, plus 2868x1320 store screenshots in `previews/run-N/store/` |
| `ios-N` | TestFlight build of 1.0.0 |
| `meta-N` | name, subtitle, privacy URL, category Racing/Simulation, age rating, description, keywords, promo text, support + marketing URL, copyright, review notes, price Free, all territories |
| `shots-N` | uploads the store screenshots (6.9-inch iPhone) to both languages |
| `attach-N` | attaches the newest processed 1.0.0 build to the version |

## Only on the website (Apple has no API for these)
1. **App Privacy**: Get Started, "No, we do not collect data from this app", Publish. (No account, no analytics, no ads; progress stays on the iPhone; Game Center and purchases are Apple's own.) Privacy Policy URL is already set.
2. **App Review contact** (if not set by REVIEW_* secrets): first and last name, phone, email.
3. **Content Rights**: App Information, Content Rights. Recommended: "No, it does not contain, show, or access third-party content" (music is your own and ElevenLabs-generated under your plan; all art made for the game).
4. **In-App Purchases**: on the 1.0 version page, "In-App Purchases and Subscriptions", Select, tick all five, Done. First purchases must go with the app version.
5. **Game Center**: on the version page, turn on Game Center and add the three leaderboards.
6. **Age rating**: check the questionnaire shows 4+ (no violence, no gambling, no chat, no user content, no ads).
7. **License Agreement**: leave as Apple's Standard EULA (our terms point to it).
8. Add for Review, Submit.
