import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ScenarioEditorPageComponent } from './scenario-editor-page.component';

describe('ScenarioEditorPageComponent', () => {
  let component: ScenarioEditorPageComponent;
  let fixture: ComponentFixture<ScenarioEditorPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScenarioEditorPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ScenarioEditorPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
