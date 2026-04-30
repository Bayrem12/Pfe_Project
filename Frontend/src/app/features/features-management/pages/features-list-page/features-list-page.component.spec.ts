import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FeaturesListPageComponent } from './features-list-page.component';

describe('FeaturesListPageComponent', () => {
  let component: FeaturesListPageComponent;
  let fixture: ComponentFixture<FeaturesListPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeaturesListPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FeaturesListPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
