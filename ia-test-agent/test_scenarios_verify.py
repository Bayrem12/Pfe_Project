"""
Quick validation script: run 3 scenarios and print pass/fail + AI analysis.
"""
import asyncio
import httpx

BASE = "http://127.0.0.1:8000"

SCENARIOS = [
    {
        "name": "SCENARIO_1_DROPDOWN_FALSE_POSITIVE",
        "desc": "Select Option2 but assert Option1  →  should FAIL",
        "url": "https://the-internet.herokuapp.com/dropdown",
        "steps": [
            {"step_type": "Given", "text": "I navigate to https://the-internet.herokuapp.com/dropdown"},
            {"step_type": "When",  "text": "I select Option 2 from the dropdown"},
            {"step_type": "Then",  "text": "I should see Option 1"},
        ],
    },
    {
        "name": "SCENARIO_2_DROPDOWN_CORRECT",
        "desc": "Select Option2 and assert Option2  →  should PASS",
        "url": "https://the-internet.herokuapp.com/dropdown",
        "steps": [
            {"step_type": "Given", "text": "I navigate to https://the-internet.herokuapp.com/dropdown"},
            {"step_type": "When",  "text": "I select Option 2 from the dropdown"},
            {"step_type": "Then",  "text": "I should see Option 2"},
        ],
    },
    {
        "name": "SCENARIO_3_LOGIN_WRONG_PASSWORD",
        "desc": "Wrong password then assert logged-in  →  should FAIL",
        "url": "https://the-internet.herokuapp.com/login",
        "steps": [
            {"step_type": "Given", "text": "I navigate to https://the-internet.herokuapp.com/login"},
            {"step_type": "When",  "text": "I enter admin in the username field"},
            {"step_type": "And",   "text": "I type wrongpassword in the password field"},
            {"step_type": "And",   "text": "I click on the login button"},
            {"step_type": "Then",  "text": "I should be logged in successfully"},
        ],
    },
]


async def run_all():
    async with httpx.AsyncClient(timeout=180) as client:
        for sc in SCENARIOS:
            print()
            print("=" * 65)
            print(f"  {sc['name']}")
            print(f"  {sc['desc']}")
            print("=" * 65)

            payload = {
                "scenario_name": sc["name"],
                "url_cible": sc["url"],
                "steps": [{"keyword": s["step_type"], "text": s["text"]} for s in sc["steps"]],
                "language": "en",
                "headless": True,
            }

            try:
                r = await client.post(f"{BASE}/api/ia/pipeline", json=payload)
                data = r.json()
            except Exception as exc:
                print(f"  HTTP ERROR: {exc}")
                continue

            overall = data.get("statut", "?")
            print(f"  OVERALL STATUS : {overall}  ({data.get('duree_ms', '?')}ms)")
            print()

            for step in data.get("steps_results", []):
                st  = step.get("statut", "?")
                nm  = (step.get("step") or "")[:70]
                err = (step.get("erreur") or "")[:120]
                sym = "✓" if st == "OK" else "✗"
                line = f"    [{sym} {st}]  {nm}"
                if err:
                    line += f"\n              ERR: {err}"
                print(line)

            # AI failure analysis per step
            gherkin = data.get("gherkin_steps_results", [])
            ai_steps = [s for s in gherkin if s.get("ai_analysis")]
            print()
            if ai_steps:
                print(f"  AI FAILURE ANALYSIS  ({len(ai_steps)} failed step(s)):")
                for s in ai_steps:
                    ai = s["ai_analysis"]
                    print(f"    step      : {(s.get('gherkin_text') or '')[:60]}")
                    print(f"    category  : {ai.get('category','?')}  (confidence={ai.get('confidence','?')})")
                    print(f"    root_cause: {(ai.get('root_cause') or '')[:90]}")
                    print(f"    fix       : {(ai.get('suggested_fix') or '')[:90]}")
                    print(f"    is_test_issue: {ai.get('is_test_issue','?')}")
                    print()
            else:
                print("  AI FAILURE ANALYSIS: none (no failed steps or all passed)")

            sc_an = data.get("scenario_analysis") or {}
            if sc_an:
                print(f"  SCENARIO SUMMARY: category={sc_an.get('category')} "
                      f"is_test_issue={sc_an.get('is_test_issue')} "
                      f"first_failed_step={sc_an.get('first_failed_step')}")


asyncio.run(run_all())
