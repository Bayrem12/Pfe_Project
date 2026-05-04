import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ScenarioDetailPageComponent } from './scenario-detail-page.component';

describe('ScenarioDetailPageComponent', () => {
  let component: ScenarioDetailPageComponent;
  let fixture: ComponentFixture<ScenarioDetailPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScenarioDetailPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ScenarioDetailPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
