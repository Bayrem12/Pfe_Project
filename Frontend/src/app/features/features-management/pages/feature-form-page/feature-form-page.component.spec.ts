import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FeatureFormPageComponent } from './feature-form-page.component';

describe('FeatureFormPageComponent', () => {
  let component: FeatureFormPageComponent;
  let fixture: ComponentFixture<FeatureFormPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeatureFormPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FeatureFormPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
